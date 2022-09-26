using System;
using System.Collections.Concurrent;
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

namespace GitTools.Editor
{
    [FilePath("UserSettings/Git/Settings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class GitSettings : ScriptableSingleton<GitSettings>
    {
        #region Public Properties
        // persistent configuration data
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
        
        // in-memory configuration data
        public static int MaxLockItems { get; set; } = 50;
        public static int ProcessTimeoutMs { get; set; } = 30_000;

        // core data
        public static bool IsGitRepo => !string.IsNullOrEmpty(instance._repoRootPath);
        public static string Branch => instance._branch;
        public static bool HasLfsProcess => instance._LfsProcessExists;
        public static bool HasLfsInRepo => instance._hasLfsConfig;
        
        // locks data
        public static IEnumerable<LfsLock> Locks => instance._Locks;
        public static LfsLockSortType LockSortType => instance._LockSortType;
        public static bool IsLockSortAscending => instance._LockSortAscending;
        public static bool AreLocksRefreshing => instance._refreshLocksTasks.Count > 0;

        // locks events
        public static event Action LocksRefreshed;
        public static event Action<LfsLock> LockStatusChanged;
        #endregion
        
        #region Private Fields
        private const double UpdateInterval = 30.0f;

        [SerializeField] private string _Username;
        [SerializeField] private string _LfsProcessName = "";
        [SerializeField] private bool _LfsProcessExists;
        [SerializeField] private List<LfsLock> _Locks = new();
        [SerializeField] private LfsLockSortType _LockSortType;
        [SerializeField] private bool _LockSortAscending;
        
        private string _repoRootPath;
        private string _branch;
        private bool _hasLfsConfig;
        private double _lastUpdateTime;
        private readonly ConcurrentDictionary<int, Task> _tasks = new();
        private readonly ConcurrentDictionary<int, Task<List<string>>> _refreshLocksTasks = new();
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

        public static void SortLocks(LfsLockSortType type, bool ascending)
        {
            instance._LockSortType = type;
            instance._LockSortAscending = ascending;
            instance.SortLocksImpl();
        }
        #endregion
        
        #region Unity Methods
        private void OnEnable()
        {
            LoadRepoInfo();
            CacheLfsProcess();
            
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += OnQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            UnityEditorFocusChanged += OnFocusChanged;

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
            WaitForTasks();
        }
        
        private void OnQuitting()
        {
            WaitForTasks();
        }

        private void OnBeforeAssemblyReload()
        {
            WaitForTasks();
        }
        
        private void OnFocusChanged(bool focused)
        {
            if (!focused)
                return;

            LoadRepoInfo();
            CacheLfsProcess();
            RefreshLocksImpl();
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

        private void WaitForTasks()
        {
            Task.WaitAll(_tasks.Values.Concat(_refreshLocksTasks.Values).ToArray());
            _tasks.Clear();
            _refreshLocksTasks.Clear();
        }
        #endregion

        #region Core
        private void LoadRepoInfo()
        {
            CacheRepoRootDir();
            LoadBranch();
            LoadLfsConfig();
        }
        
        private void CacheRepoRootDir()
        {
            if (!string.IsNullOrEmpty(_repoRootPath))
            {
                // if this is no longer a git repo, then clear out the repo info
                if (!Directory.Exists(_repoRootPath))
                    _repoRootPath = string.Empty;
                return;
            }
            
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
        
        private void LoadBranch()
        {
            _branch = string.Empty;
            
            if (!IsGitRepo)
                return;
            
            var headPath = Path.Combine(_repoRootPath, ".git", "HEAD");
            if (!File.Exists(headPath))
                throw new Exception($"[Git] Failed to load Git branch from \"{headPath}\"");
            
            var line = File.ReadLines(headPath).First();
            _branch = line.Replace("ref: refs/heads/", "");
        }
        
        private void LoadLfsConfig()
        {
            _hasLfsConfig = false;
            
            if (!IsGitRepo)
                return;
            
            var configPath = Path.Combine(_repoRootPath, ".git", "config");
            if (!File.Exists(configPath))
                throw new Exception($"[Git] Failed to load Git config \"{configPath}\"");

            var text = File.ReadAllText(configPath);
            _hasLfsConfig = text.Contains("[lfs]");
        }
        #endregion
        
        #region LFS
        private void LockImpl(string path)
        {
            if (Locks.Any(lfsLock => lfsLock.Path == path))
                return;
            
            var lfsLock = new LfsLock
            {
                Path = path,
                AssetGuid = AssetDatabase.AssetPathToGUID(path),
                User = Username,
                IsPending = true,
            };
            _Locks.Add(lfsLock);
            LockStatusChanged?.Invoke(lfsLock);
            
            var task = Task.Run(() => InvokeLfs($"lock \"{path}\""));
            _tasks[task.Id] = task;

            RefreshLocksImpl(task);
        }

        private void UnlockImpl(string id, bool force)
        {
            var lfsLock = Locks.First(lfsLock => lfsLock.Id == id);
            if (lfsLock.IsPending)
                return;
            
            lfsLock.IsPending = true;
            LockStatusChanged?.Invoke(lfsLock);
            
            var task = Task.Run(() => InvokeLfs($"unlock --id {id}" + (force ? " --force" : "")));
            _tasks[task.Id] = task;

            RefreshLocksImpl(task);
        }
        
        private void RefreshLocksImpl(Task priorTask = null)
        {
            Task<List<string>> task;
            if (priorTask == null)
            {
                task = Task.Run(() => InvokeLfs("locks"));
            }
            else
            {
                task = priorTask.ContinueWith(t =>
                {
                    _tasks.TryRemove(t.Id, out _);
                    return InvokeLfs("locks");
                });
            }
            _refreshLocksTasks[task.Id] = task;

            task.ContinueWith(t =>
            {
                _refreshLocksTasks.TryRemove(t.Id, out _);
                ProcessLocksResult(t.Result);
            },
            TaskScheduler.FromCurrentSynchronizationContext());
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

            SortLocksImpl();

            Save();
            
            LocksRefreshed?.Invoke();
        }

        private void SortLocksImpl()
        {
            _Locks.Sort(CompareLocks);

            int CompareLocks(LfsLock x, LfsLock y)
            {
                if (!_LockSortAscending)
                    (y, x) = (x, y);
                
                switch (_LockSortType)
                {
                    case LfsLockSortType.User:
                        var result = string.Compare(x.User, y.User, StringComparison.CurrentCultureIgnoreCase);
                        if (result != 0)
                            return result;
                        goto case LfsLockSortType.Path;
                        
                        case LfsLockSortType.Path:
                            return string.Compare(x.Path, y.Path, StringComparison.InvariantCultureIgnoreCase);
                        
                        case LfsLockSortType.Id:
                            return string.Compare(x.Id, y.Id, StringComparison.InvariantCultureIgnoreCase);
                        
                        default:
                            throw new NotImplementedException();
                };
            }
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

        private void CacheLfsProcess()
        {
            if (!string.IsNullOrWhiteSpace(_LfsProcessName))
                return;
            
            // On Windows, running lfs is our best bet to check if it exists
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                _LfsProcessName = "git-lfs";
                
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo(_LfsProcessName, "version")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    },
                };
                try
                {
                    _LfsProcessExists = process.Start();
                }
                catch (Exception e)
                {
                    _LfsProcessExists = (e is not ThreadAbortException);
                }
                finally
                {
                    if (!_LfsProcessExists)
                        _LfsProcessName = string.Empty;
                }
                return;
            }
            
            // On Mac, Homebrew installs packages to a different folder on ARM (M series) vs Intel.
            // Homebrew also asks you to inject path vars into your shell profile, which we don't
            // have access to when we run Process.Start, since we want to redirect stdout.
            //
            // The hacky thing done here is guessing where your LFS is installed by checking both
            // paths. Invoking "command -v git-lfs" is a less-hacky-but-less-performant option.
            // 
            // More on Homebrew diffs between Intel/ARM: https://apple.stackexchange.com/a/93179
            // More on Process.Start paths in C#: https://stackoverflow.com/a/41318134
            _LfsProcessName = "/opt/homebrew/bin/git-lfs";
            if (!File.Exists(_LfsProcessName))
                _LfsProcessName = "/usr/local/bin/git-lfs";

            _LfsProcessExists = File.Exists(_LfsProcessName);
        }
        #endregion
    }
}
