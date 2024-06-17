using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MikeSchweitzer.Git.Editor
{
    public static class AssetsMenuExtensions
    {
        #region Private Fields
        private static List<Object> SelectionAssets { get; } = new List<Object>();
        #endregion

        #region Menu Methods
        [MenuItem("Assets/Git Lock", isValidateFunction: true)]
        private static bool ValidateGitLock()
        {
            if (!GitSettings.HasUsername)
                return true;

            var objects = GetDeepAssets();
            if (objects.Length > GitSettings.MaxLockItems)
                return false;

            var guids = objects.Select(obj =>
            {
                var path = AssetDatabase.GetAssetPath(obj);
                return AssetDatabase.AssetPathToGUID(path);
            });

            return guids.Any(guid => GitSettings.Locks.All(lfsLock => lfsLock._AssetGuid != guid));
        }

        [MenuItem("Assets/Git Lock", priority = 10_000)]
        private static void GitLock()
        {
            if (ShowUsernameEntryIfNeeded())
                return;

            var objects = GetDeepAssets();
            foreach (var obj in objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                var guid = AssetDatabase.AssetPathToGUID(path);

                if (GitSettings.Locks.Any(lfsLock => lfsLock._AssetGuid == guid))
                    continue;

                GitSettings.Lock(path);
            }
        }

        [MenuItem("Assets/Git Unlock", isValidateFunction: true)]
        private static bool ValidateGitUnlock()
        {
            if (!GitSettings.HasUsername)
                return true;

            var objects = GetDeepAssets();
            if (objects.Length > GitSettings.MaxLockItems)
                return false;

            var guids = objects.Select(obj =>
            {
                var path = AssetDatabase.GetAssetPath(obj);
                return AssetDatabase.AssetPathToGUID(path);
            });

            return guids.Any(guid =>
            {
                return GitSettings.Locks.Any(lfsLock =>
                    !lfsLock._IsPending &&
                    lfsLock._AssetGuid == guid &&
                    lfsLock._User == GitSettings.Username);
            });
        }

        [MenuItem("Assets/Git Unlock", priority = 10_000)]
        private static void GitUnlock()
        {
            if (ShowUsernameEntryIfNeeded())
                return;

            var objects = GetDeepAssets();
            foreach (var obj in objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                var guid = AssetDatabase.AssetPathToGUID(path);

                var lfsLock = GitSettings.Locks.FirstOrDefault(lfsLock =>
                    !lfsLock._IsPending &&
                    lfsLock._AssetGuid == guid &&
                    lfsLock._User == GitSettings.Username);
                if (lfsLock == null)
                    continue;

                GitSettings.Unlock(lfsLock._Id);
            }
        }

        [MenuItem("Assets/Git Force Unlock", isValidateFunction: true)]
        private static bool ValidateGitForceUnlock()
        {
            if (!GitSettings.HasUsername)
                return true;

            var objects = GetDeepAssets();
            if (objects.Length > GitSettings.MaxLockItems)
                return false;

            var guids = objects.Select(obj =>
            {
                var path = AssetDatabase.GetAssetPath(obj);
                return AssetDatabase.AssetPathToGUID(path);
            });

            return guids.Any(guid => GitSettings.Locks.Any(lfsLock =>
                !lfsLock._IsPending &&
                lfsLock._AssetGuid == guid));
        }

        [MenuItem("Assets/Git Force Unlock", priority = 10_000)]
        private static void GitForceUnlock()
        {
            if (ShowUsernameEntryIfNeeded())
                return;

            if (!GitLocksEditor.DisplayForceUnlockConfirmationDialog())
                return;

            var objects = GetDeepAssets();
            foreach (var obj in objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                var guid = AssetDatabase.AssetPathToGUID(path);

                var lfsLock = GitSettings.Locks.FirstOrDefault(lfsLock =>
                    !lfsLock._IsPending &&
                    lfsLock._AssetGuid == guid);
                if (lfsLock == null)
                    continue;

                GitSettings.ForceUnlock(lfsLock._Id);
            }
        }
        #endregion

        #region Helper Methods
        private static bool ShowUsernameEntryIfNeeded()
        {
            if (GitSettings.HasUsername)
                return false;

            EditorUtility.DisplayDialog("No Git Username",
                "Enter your username for this project's Git hosting site (aka Git remote)",
                "OK");

            GitLocksEditor.ShowWindow();
            return true;
        }

        private static Object[] GetDeepAssets()
        {
            SelectionAssets.Clear();

            var objects = Selection.GetFiltered<Object>(SelectionMode.DeepAssets);
            foreach (var obj in objects)
            {
                // folders are of type DefaultAsset
                if (obj is DefaultAsset)
                    continue;

                SelectionAssets.Add(obj);
            }

            return SelectionAssets.ToArray();
        }
        #endregion
    }
}
