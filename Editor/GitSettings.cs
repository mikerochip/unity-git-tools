using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
        
        #region Private Properties
        // example "git lfs locks" result for cloud service (e.g. GitHub.com) and
        // self-hosted service, respectively
        // Assets/foo.png   	username                	ID:123456
        // Assets/foobar.png	Foo Bar (fbar@example.com)	ID:123456
        private static Regex LocksResultRegex { get; } = new (
            @"^(?<path>.+)\b\s*\t(.+\((?<user>\S+)@\S+\)|(?<user>\S+))\s*\tID:(?<id>\S+)$", 
            RegexOptions.Compiled);
        #endregion
        
        #region Private Fields
        private const double UpdateInterval = 30.0f;
        private const int ProcessTimeoutMs = 30_000;

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
        private bool _stopAutoRefreshLocks;
        private readonly ConcurrentDictionary<int, Task<ProcessResult>> _tasks = new();
        private readonly ConcurrentDictionary<int, Task<ProcessResult>> _refreshLocksTasks = new();
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

        public static void ForceRefreshLocks()
        {
            const string title = "Refresh Git Locks";
            
            EditorUtility.DisplayProgressBar(title, "Clearing cache", 0.0f);
            instance._Locks.Clear();
            LocksRefreshed?.Invoke();
            
            EditorUtility.DisplayProgressBar(title, "Waiting for Git tasks", 0.3f);
            instance.WaitForTasks();
            
            EditorUtility.DisplayProgressBar(title, "Requesting locks", 0.6f);
            var result = instance.InvokeLfs("locks");
            instance.ProcessLocksResult(result);
            
            EditorUtility.ClearProgressBar();
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
            _stopAutoRefreshLocks = false;
            
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
            
            if (!AreLocksRefreshing && !_stopAutoRefreshLocks)
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
            
            if (!AreLocksRefreshing && !_stopAutoRefreshLocks)
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
            var tasks = new Task[_tasks.Count + _refreshLocksTasks.Count];
            var i = 0;
            
            foreach (var task in _tasks.Values)
                tasks[i++] = task;
            _tasks.Clear();
            
            foreach (var task in _refreshLocksTasks.Values)
                tasks[i++] = task;
            _refreshLocksTasks.Clear();
            
            Task.WaitAll(tasks);
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
            if (Locks.Any(lfsLock => lfsLock._Path == path))
                return;
            
            var lfsLock = new LfsLock
            {
                _Path = path,
                _AssetGuid = AssetDatabase.AssetPathToGUID(path),
                _User = Username,
                _IsPending = true,
            };
            _Locks.Add(lfsLock);
            LockStatusChanged?.Invoke(lfsLock);
            
            var task = Task.Run(() => InvokeLfs($"lock \"{path}\""));
            _tasks[task.Id] = task;

            RefreshLocksImpl(task);
        }

        private void UnlockImpl(string id, bool force)
        {
            var lfsLock = Locks.First(lfsLock => lfsLock._Id == id);
            if (lfsLock._IsPending)
                return;
            
            lfsLock._IsPending = true;
            LockStatusChanged?.Invoke(lfsLock);
            
            var task = Task.Run(() => InvokeLfs($"unlock --id {id}" + (force ? " --force" : "")));
            _tasks[task.Id] = task;

            RefreshLocksImpl(task);
        }
        
        private void RefreshLocksImpl(Task<ProcessResult> priorTask = null)
        {
            Task<ProcessResult> task;
            if (priorTask == null)
            {
                task = Task.Run(() => InvokeLfs("locks"));
            }
            else
            {
                task = priorTask.ContinueWith(t =>
                {
                    _tasks.TryRemove(t.Id, out _);

                    // if the priorTask had errors, then stop
                    if (t.Result.ErrorLines.Count > 0)
                        return null;
                    
                    // if other tasks are in flight, then listing locks is a waste
                    // we only need to list locks after the last task is done
                    if (_tasks.Count > 0)
                        return null;
                    
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

        private void ProcessLocksResult(ProcessResult result)
        {
            _Locks.Clear();

            if (result == null || result.ErrorLines.Count > 0)
            {
                _stopAutoRefreshLocks = true;
            }
            else
            {
                _stopAutoRefreshLocks = false;
                
                foreach (var line in result.OutLines)
                {
                    var match = LocksResultRegex.Match(line);
                    var lfsLock = new LfsLock
                    {
                        _Path = match.Groups["path"].Value,
                        _User = match.Groups["user"].Value,
                        _Id = match.Groups["id"].Value,
                    };
                    lfsLock._AssetGuid = AssetDatabase.AssetPathToGUID(lfsLock._Path);
                    _Locks.Add(lfsLock);
                }
                
                SortLocksImpl();
            }

            Save();
            
            LocksRefreshed?.Invoke();
        }

        private void SortLocksImpl()
        {
            _Locks.Sort((x, y) =>
            {
                if (!_LockSortAscending)
                    (y, x) = (x, y);

                switch (_LockSortType)
                {
                    case LfsLockSortType.User:
                        var result = EditorUtility.NaturalCompare(x._User, y._User);
                        if (result != 0)
                            return result;
                        goto case LfsLockSortType.Path;

                    case LfsLockSortType.Path:
                        return PathCompare(x._Path, y._Path);

                    case LfsLockSortType.Id:
                        return EditorUtility.NaturalCompare(x._Id, y._Id);

                    default:
                        throw new NotImplementedException();
                }
            });
        }

        private static int PathCompare(string x, string y)
        {
            var minLength = Math.Min(x.Length, y.Length);
            for (var i = 0; i < minLength; ++i)
            {
                var cx = x[i];
                var cy = y[i];
                if (cx == cy)
                    continue;

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // on Windows, folders always sort above files in the same directory
                    var xNextSeparator = FindNextPathSeparator(x, i);
                    var yNextSeparator = FindNextPathSeparator(y, i);
                    var xIsFolder = x[xNextSeparator] is '/' or '\\';
                    var yIsFolder = y[yNextSeparator] is '/' or '\\';
                    
                    if (xIsFolder && yIsFolder)
                    {
                        // maybe a Unity bug...
                        // 
                        // 2 folders has a weird special case where NaturalCompare is ignored:
                        // Foo
                        // Foo 1
                        // Foo 10
                        // 
                        // NaturalCompare does this (this is also how files sort):
                        // Foo 1
                        // Foo 10
                        // Foo
                        if (x[i] is '/' or '\\')
                            return -1;
                        if (y[i] is '/' or '\\')
                            return 1;
                    }
                    
                    if (xIsFolder && !yIsFolder)
                        return -1;
                    if (yIsFolder && !xIsFolder)
                        return 1;
                }
                else
                {
                    // on Mac/Linux, folders only sort above files with the same name
                    if (cx is '/' or '\\')
                        return -1;
                    if (cy is '/' or '\\')
                        return 1;
                }
                break;
            }
            return EditorUtility.NaturalCompare(x, y);

            static int FindNextPathSeparator(string s, int startIndex)
            {
                for (var i = startIndex; i < s.Length; ++i)
                {
                    var c = s[i];
                    if (c is '/' or '\\')
                        return i;
                }
                return s.Length - 1;
            }
        }

        private ProcessResult InvokeLfs(string args)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(_LfsProcessName, args)
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = _repoRootPath,
                },
            };

            var processResult = new ProcessResult();
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (string.IsNullOrEmpty(eventArgs.Data))
                    return;
                processResult.OutLines.Add(eventArgs.Data);
            };
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (string.IsNullOrEmpty(eventArgs.Data))
                    return;
                Debug.LogError($"[Git] LfsCommand=\"{args}\"|Error=\"{eventArgs.Data}\"");
                processResult.ErrorLines.Add(eventArgs.Data);
            };
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(ProcessTimeoutMs))
                {
                    // Process is still going, kill it. To prevent a shutdown race condition,
                    // we have to try/catch.
                    // See https://blog.yaakov.online/waiting-for-a-process-with-timeout-in-net/
                    try
                    {
                        var error = $"Timed out after {ProcessTimeoutMs / 1000.0f}s";
                        Debug.LogError($"[Git] LfsCommand=\"{args}\"|Error=\"{error}\"");
                        processResult.ErrorLines.Add(error);
                        process.Kill();
                        return processResult;
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
            catch (Exception e)
            {
                // Don't log ThreadAbortException since this means the engine is killing
                // our thread in order to do domain reloads or other engine-y things.
                // That's fine, git operations can just be re-triggered.
                if (e is not ThreadAbortException)
                {
                    Debug.LogError($"[Git] LfsCommand=\"{args}\"" +
                                   $"|Exception={e.GetType()}" +
                                   $"|Message=\"{e.Message}\"");
                    throw;
                }
                
                // also don't stop the train for the fact that a git lfs command failed,
                // the user can redo it, it's low stakes enough
                Thread.ResetAbort();
            }

            // We call this again to wait for async events to finish.
            // See https://learn.microsoft.com/en-us/dotnet/api/System.Diagnostics.Process.WaitForExit
            // "To ensure that asynchronous event handling has been completed, call the
            // WaitForExit() overload that takes no parameter after receiving a true from
            // this overload."
            process.WaitForExit();
            return processResult;
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
