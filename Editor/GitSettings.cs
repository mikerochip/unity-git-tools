using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace GitGoodies.Editor
{
    [FilePath("UserSettings/GitGoodies/Settings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class GitSettings : ScriptableSingleton<GitSettings>
    {
        #region Public Properties
        public const int MaxLockItems = 50;
        public const int ProcessTimeoutMs = 30_000;

        public static string Username
        {
            get => instance._Username;
            set
            {
                instance._Username = value;
                Save();
            }
        }
        public static bool HasUsername => !string.IsNullOrWhiteSpace(Username);
        public static string Branch => instance._branch;
        public static IEnumerable<LfsLock> Locks => instance._Locks;

        public static event EventHandler LocksRefreshed;
        #endregion
        
        #region Private Fields
        private const double UpdateInterval = 30.0f;

        [SerializeField] private string _Username;
        [SerializeField] private List<LfsLock> _Locks = new();
        [SerializeField] private string _LfsProcessName = "";
        
        private string _repoRootPath;
        private string _branch;
        private double _lastUpdateTime;
        private Task<List<string>> _refreshLocksTask;
        #endregion
        
        #region Public Methods
        public static void Lock(string assetPath)
        {
            instance.LockImpl(assetPath);
        }

        public static void Unlock(string id)
        {
            instance.UnlockImpl(id, false);
        }

        public static void ForceUnlock(string id)
        {
            instance.UnlockImpl(id, true);
        }
        
        public static void RefreshLocks()
        {
            instance.RefreshLocksImpl();
        }
        #endregion
        
        #region Unity Methods
        private void OnEnable()
        {
            CacheRepoRootDir();
            LoadBranch();
            CacheLfsProcessName();
            
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityEditorFocusChanged += OnFocusChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // need to call this AFTER installing unity event hooks or else async tasks can lock
            // up the editor
            RefreshLocksImpl();
        }
        #endregion

        #region Event Handlers
        private void OnUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastUpdateTime < UpdateInterval)
                return;

            _lastUpdateTime = now;
            
            LoadBranch();
            RefreshLocksImpl();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            _refreshLocksTask?.Wait();
        }
        
        private void OnFocusChanged(bool focused)
        {
            if (!focused)
                return;
            
            LoadBranch();
            RefreshLocksImpl();
        }

        private void OnBeforeAssemblyReload()
        {
            _refreshLocksTask?.Wait();
        }
        
        // see http://answers.unity.com/answers/1886639/view.html
        private static Action<bool> UnityEditorFocusChanged
        {
            get
            {
                var fieldInfo = typeof(EditorApplication).GetField("focusChanged",
                    BindingFlags.Static | BindingFlags.NonPublic);
                return (Action<bool>)fieldInfo!.GetValue(null);
            }
            set
            {
                var fieldInfo = typeof(EditorApplication).GetField("focusChanged",
                    BindingFlags.Static | BindingFlags.NonPublic);
                fieldInfo!.SetValue(null, value);
            }
        }
        #endregion
        
        #region Helpers
        private static void Save()
        {
            instance.Save(saveAsText: true);
        }

        private void CacheRepoRootDir()
        {
            if (!string.IsNullOrEmpty(_repoRootPath))
                return;
            
            var directory = Directory.GetParent(Application.dataPath);
            while (directory is { Exists: true })
            {
                var gitPath = Path.Combine(directory.FullName, ".git");
                if (Directory.Exists(gitPath))
                {
                    _repoRootPath = directory.FullName;
                    break;
                }

                directory = directory.Parent;
            }
        }
        #endregion
        
        #region Branch
        private void LoadBranch()
        {
            var headPath = Path.Combine(_repoRootPath, ".git", "HEAD");
            if (!File.Exists(headPath))
                throw new Exception($"[Git] Failed to load Git branch from \"{headPath}\"");
            
            var line = File.ReadLines(headPath).First();
            _branch = line.Replace("ref: refs/heads/", "");
        }
        #endregion
        
        #region LFS
        private void LockImpl(string path)
        {
            Task.Run(() =>
            {
                InvokeLfs($"lock {path}");
            }).ContinueWith(_ =>
            {
                return InvokeLfs("locks");
            }).ContinueWith(task =>
            {
                ProcessLocksResult(task.Result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void UnlockImpl(string id, bool force)
        {
            Task.Run(() =>
            {
                InvokeLfs($"unlock --id {id}" + (force ? " --force" : ""));
            }).ContinueWith(_ =>
            {
                return InvokeLfs("locks");
            }).ContinueWith(task =>
            {
                ProcessLocksResult(task.Result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        
        private void RefreshLocksImpl()
        {
            _refreshLocksTask = Task.Run(() => InvokeLfs("locks"));
            
            _refreshLocksTask.ContinueWith(task =>
            {
                ProcessLocksResult(task.Result);
                _refreshLocksTask = null;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ProcessLocksResult(List<string> results)
        {
            _Locks.Clear();
            
            foreach (var result in results)
            {
                // this is an example of a result:
                // Assets/foo.png	mikerochip	ID:1436853
                var parts = result.Split('\t');
                var lfsLock = new LfsLock
                {
                    Path = parts[0].Trim(),
                    User = parts[1].Trim(),
                    Id = parts[2].Trim().Replace("ID:", ""),
                };
                lfsLock.AssetGuid = AssetDatabase.AssetPathToGUID(lfsLock.Path);
                _Locks.Add(lfsLock);
            }

            Save();
            
            LocksRefreshed?.Invoke(this, EventArgs.Empty);
        }

        private List<string> InvokeLfs(string args)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(_LfsProcessName, args)
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WorkingDirectory = _repoRootPath,
                },
            };
            
            var outputLines = new List<string>();
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (string.IsNullOrEmpty(eventArgs.Data))
                    return;
                outputLines.Add(eventArgs.Data);
            };
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit(ProcessTimeoutMs);
            }
            catch (Exception e)
            {
                // Don't log ThreadAbortException since this means the engine is killing
                // our thread in order to do domain reloads or other engine-y things.
                // That's fine, git operations can just be re-triggered.
                if (e is not ThreadAbortException)
                {
                    Debug.LogError($"[Git] Failed to run \"{_LfsProcessName} {args}\"\n" +
                                   $"Exception: {e.GetType()}\n" +
                                   $"Message: {e.Message}");
                    throw;
                }
                
                // also don't stop the train for the fact that a git lfs command failed,
                // the user can redo it, it's low stakes enough
                Thread.ResetAbort();
            }
            return outputLines;
        }

        private void CacheLfsProcessName()
        {
            if (!string.IsNullOrWhiteSpace(_LfsProcessName))
                return;
            
            // On Windows, lfs should be able to be invoked without a full path
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                _LfsProcessName = "git-lfs";
                return;
            }
            
            // On Mac, Homebrew installs packages to a different folder on ARM (M series) vs Intel.
            // Homebrew also asks you to inject path vars into your shell profile, which we don't
            // have access to when we run Process.Start, since we want to redirect stdout.
            //
            // So the hacky thing we do here is try to guess where your LFS is installed with the
            // ARM and Intel paths. We could invoke "command -v" but that would cost extra invokes.
            // 
            // More on Homebrew diffs between Intel/ARM: https://apple.stackexchange.com/a/93179
            // More on Process.Start paths in C#: https://stackoverflow.com/a/41318134
            _LfsProcessName = "/opt/homebrew/bin/git-lfs";
            if (!File.Exists(_LfsProcessName))
                _LfsProcessName = "/usr/local/bin/git-lfs";
        }
        #endregion
    }
}