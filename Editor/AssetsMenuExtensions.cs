using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GitTools.Editor
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
                return false;
            
            var objects = GetDeepAssets();
            if (objects.Length > GitSettings.MaxLockItems)
                return false;

            var guids = objects.Select(obj =>
            {
                var path = AssetDatabase.GetAssetPath(obj);
                return AssetDatabase.AssetPathToGUID(path);
            });

            return guids.Any(guid => GitSettings.Locks.All(lfsLock => lfsLock.AssetGuid != guid));
        }
        
        [MenuItem("Assets/Git Lock", priority = 10_000)]
        private static void GitLock()
        {
            var objects = GetDeepAssets();
            foreach (var obj in objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                var guid = AssetDatabase.AssetPathToGUID(path);

                if (GitSettings.Locks.Any(lfsLock => lfsLock.AssetGuid == guid))
                    continue;
                
                GitSettings.Lock(path);
            }
        }
        
        [MenuItem("Assets/Git Unlock", isValidateFunction: true)]
        private static bool ValidateGitUnlock()
        {
            if (!GitSettings.HasUsername)
                return false;
            
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
                    !lfsLock.IsPending &&
                    lfsLock.AssetGuid == guid &&
                    lfsLock.User == GitSettings.Username);
            });
        }
        
        [MenuItem("Assets/Git Unlock", priority = 10_000)]
        private static void GitUnlock()
        {
            var objects = GetDeepAssets();
            foreach (var obj in objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                var guid = AssetDatabase.AssetPathToGUID(path);

                var lfsLock = GitSettings.Locks.FirstOrDefault(lfsLock =>
                    !lfsLock.IsPending &&
                    lfsLock.AssetGuid == guid &&
                    lfsLock.User == GitSettings.Username);
                if (lfsLock == null)
                    continue;
                
                GitSettings.Unlock(lfsLock.Id);
            }
        }
        
        [MenuItem("Assets/Git Force Unlock", isValidateFunction: true)]
        private static bool ValidateGitForceUnlock()
        {
            if (!GitSettings.HasUsername)
                return false;
            
            var objects = GetDeepAssets();
            if (objects.Length > GitSettings.MaxLockItems)
                return false;

            var guids = objects.Select(obj =>
            {
                var path = AssetDatabase.GetAssetPath(obj);
                return AssetDatabase.AssetPathToGUID(path);
            });

            return guids.Any(guid => GitSettings.Locks.Any(lfsLock => 
                !lfsLock.IsPending &&
                lfsLock.AssetGuid == guid));
        }
        
        [MenuItem("Assets/Git Force Unlock", priority = 10_000)]
        private static void GitForceUnlock()
        {
            var objects = GetDeepAssets();
            foreach (var obj in objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                var guid = AssetDatabase.AssetPathToGUID(path);

                var lfsLock = GitSettings.Locks.FirstOrDefault(lfsLock =>
                    !lfsLock.IsPending &&
                    lfsLock.AssetGuid == guid);
                if (lfsLock == null)
                    continue;
                
                GitSettings.ForceUnlock(lfsLock.Id);
            }
        }
        #endregion
        
        #region Helper Methods
        public static Object[] GetDeepAssets()
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