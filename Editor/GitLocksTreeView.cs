using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

using Object = UnityEngine.Object;

namespace GitTools.Editor
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
            
            EditorGUI.BeginDisabledGroup(lfsLock._IsPending);
            
            for (var i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                var colType = (LfsLockColumnType)args.GetColumn(i);
                var rect = args.GetCellRect(i);

                switch (colType)
                {
                    case LfsLockColumnType.User:
                        EditorGUI.LabelField(rect, lfsLock._User);
                        break;
                    
                    case LfsLockColumnType.Path:
                    {
                        if (!GitLocksEditorSettings.IsShowingPlainTextPaths)
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<Object>(lfsLock._Path);
                            if (asset != null)
                            {
                                EditorGUI.ObjectField(rect, asset, asset.GetType(), allowSceneObjects: false);
                                break;
                            }
                        }
                        EditorGUI.LabelField(rect, lfsLock._Path);
                        break;
                    }
                    
                    case LfsLockColumnType.Id:
                        EditorGUI.LabelField(rect, lfsLock._Id);
                        break;
                    
                    default:
                        throw new NotImplementedException();
                }
            }
            
            EditorGUI.EndDisabledGroup();
        }

        protected override void ContextClicked()
        {
            var menu = new GenericMenu();
            
            AddContextMenuUnlockItems(menu);
            menu.AddSeparator("");
            AddContextMenuSettings(menu);
            
            menu.ShowAsContext();
            Event.current.Use();
        }

        protected override void ContextClickedItem(int id)
        {
            var menu = new GenericMenu();
            
            AddContextMenuUnlockItems(menu);
            menu.AddSeparator("");
            AddContextMenuCopyItems(menu, id);
            AddContextMenuSettings(menu);
            
            menu.ShowAsContext();
            Event.current.Use();
        }
        #endregion

        #region Private Methods
        private void AddContextMenuUnlockItems(GenericMenu menu)
        {
            var lfsLocks = new List<LfsLock>();
            var ownedLfsLocks = new List<LfsLock>();
            foreach (var id in state.selectedIDs)
            {
                var lfsLock = _locks[id];
                if (lfsLock._IsPending)
                    continue;
                
                lfsLocks.Add(lfsLock);
                if (lfsLock._User == GitSettings.Username)
                    ownedLfsLocks.Add(lfsLock);
            }

            if (ownedLfsLocks.Count > 0)
            {
                menu.AddItem(new GUIContent("Unlock"), false, () =>
                {
                    foreach (var lfsLock in ownedLfsLocks)
                        GitSettings.Unlock(lfsLock._Id);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Unlock"), false);
            }

            if (lfsLocks.Count > 0)
            {
                menu.AddItem(new GUIContent("Force Unlock"), false, () =>
                {
                    foreach (var lfsLock in lfsLocks)
                        GitSettings.ForceUnlock(lfsLock._Id);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Force Unlock"), false);
            }
        }

        private void AddContextMenuCopyItems(GenericMenu menu, int id)
        {
            var lfsLock = _locks[id];
            
            menu.AddItem(new GUIContent($"Copy/User"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock._User);
            
            menu.AddItem(new GUIContent($"Copy/Asset Path"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock._Path);
            
            menu.AddItem(new GUIContent($"Copy/Asset GUID"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock._AssetGuid);

            if (!string.IsNullOrEmpty(lfsLock._Id))
            {
                menu.AddItem(new GUIContent($"Copy/Lock ID"), false, () => 
                    GUIUtility.systemCopyBuffer = lfsLock._Id);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent($"Copy/Lock ID"), false);
            }
        }

        private void AddContextMenuSettings(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Settings/Plain Text Paths"), GitLocksEditorSettings.IsShowingPlainTextPaths, () =>
            {
                GitLocksEditorSettings.IsShowingPlainTextPaths = !GitLocksEditorSettings.IsShowingPlainTextPaths;
            });
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