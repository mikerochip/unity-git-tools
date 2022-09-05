using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

using Object = UnityEngine.Object;

namespace GitGoodies.Editor
{
    public class GitLocksTreeView : TreeView
    {
        #region Private Fields
        private LfsLock[] _locks;
        #endregion
        
        #region TreeView Methods
        public GitLocksTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader)
            : base(state, multiColumnHeader)
        {
            useScrollView = true;
            multiColumnHeader.ResizeToFit();
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.visibleColumnsChanged += OnVisibleColumnsChanged;
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem
            {
                id = 0,
                depth = -1,
                displayName = "Root",
            };

            _locks = GitSettings.Locks.ToArray();
            for (var i = 0; i < _locks.Length; ++i)
            {
                var item = new TreeViewItem
                {
                    id = i,
                    displayName = i.ToString(),
                };
                root.AddChild(item);
            }
            if (_locks.Length > 0)
                SetupDepthsFromParentsAndChildren(root);
            
            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var lfsLock = _locks[args.item.id];
            
            for (var i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                var colType = (LfsLockColumnType)args.GetColumn(i);
                var rect = args.GetCellRect(i);

                switch (colType)
                {
                    case LfsLockColumnType.User:
                        EditorGUI.LabelField(rect, lfsLock.User);
                        break;
                    
                    case LfsLockColumnType.Path:
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(lfsLock.Path);
                        if (asset == null)
                            EditorGUI.LabelField(rect, lfsLock.Path);
                        else
                            EditorGUI.ObjectField(rect, asset, asset.GetType(), allowSceneObjects: false);
                        break;
                    }
                    
                    case LfsLockColumnType.Id:
                        EditorGUI.LabelField(rect, lfsLock.Id);
                        break;
                    
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        protected override void ContextClicked()
        {
            var menu = new GenericMenu();
            
            AddUnlockItems(menu);
            
            menu.ShowAsContext();
            Event.current.Use();
        }

        protected override void ContextClickedItem(int id)
        {
            var lfsLock = _locks[id];
            
            var menu = new GenericMenu();
            
            AddUnlockItems(menu);
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent($"Copy/User"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock.User);
            menu.AddItem(new GUIContent($"Copy/Asset Path"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock.Path);
            menu.AddItem(new GUIContent($"Copy/Asset GUID"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock.AssetGuid);
            menu.AddItem(new GUIContent($"Copy/Lock ID"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock.Id);
            
            menu.ShowAsContext();
            Event.current.Use();
        }
        #endregion

        #region Private Methods
        private void AddUnlockItems(GenericMenu menu)
        {
            var lfsLocks = new List<LfsLock>();
            foreach (var id in state.selectedIDs)
                lfsLocks.Add(_locks[id]);
            
            if (lfsLocks.Any(lfsLock => lfsLock.User == GitSettings.Username))
            {
                menu.AddItem(new GUIContent("Unlock"), false, () =>
                {
                    foreach (var lfsLock in lfsLocks)
                        GitSettings.Unlock(lfsLock.Id);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Unlock"), false);
            }

            if (lfsLocks.Any())
            {
                menu.AddItem(new GUIContent("Force Unlock"), false, () =>
                {
                    foreach (var lfsLock in lfsLocks)
                        GitSettings.ForceUnlock(lfsLock.Id);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Force Unlock"), false);
            }
        }
        #endregion

        #region Column Event Handlers
        private void OnSortingChanged(MultiColumnHeader _)
        {
            var ascending = multiColumnHeader.IsSortedAscending(multiColumnHeader.sortedColumnIndex);
            
            switch ((LfsLockColumnType)multiColumnHeader.sortedColumnIndex)
            {
                case LfsLockColumnType.User:
                    GitSettings.SortLocks(LfsLockSortType.User, ascending);
                    break;
                
                case LfsLockColumnType.Path:
                    GitSettings.SortLocks(LfsLockSortType.Path, ascending);
                    break;
                
                case LfsLockColumnType.Id:
                    GitSettings.SortLocks(LfsLockSortType.Id, ascending);
                    break;
            }
            
            Reload();
        }

        private void OnVisibleColumnsChanged(MultiColumnHeader _)
        {
            multiColumnHeader.ResizeToFit();
        }
        #endregion
    }
}