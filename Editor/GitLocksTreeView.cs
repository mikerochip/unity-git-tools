using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

using Object = UnityEngine.Object;

namespace MikeSchweitzer.Git.Editor
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
                    id = IndexToId(i),
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
            var lfsLock = _locks[args.row];

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
        private static int IndexToId(int index) => index + 1;
        private static int IdToIndex(int id) => id - 1;

        private void AddContextMenuUnlockItems(GenericMenu menu)
        {
            var othersLocks = new List<LfsLock>();
            var myLocks = new List<LfsLock>();
            foreach (var id in state.selectedIDs)
            {
                // still can't quite track this down, but this has happened to me
                var index = IdToIndex(id);
                if (index >= _locks.Length)
                    continue;

                var lfsLock = _locks[index];
                if (lfsLock._IsPending)
                    continue;

                if (lfsLock._User == GitSettings.Username)
                    myLocks.Add(lfsLock);
                else
                    othersLocks.Add(lfsLock);
            }

            if (myLocks.Count > 0)
            {
                menu.AddItem(new GUIContent("Unlock"), false, () =>
                {
                    foreach (var lfsLock in myLocks)
                        GitSettings.Unlock(lfsLock._Id);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Unlock"), false);
            }

            if (myLocks.Count > 0 || othersLocks.Count > 0)
            {
                menu.AddItem(new GUIContent("Force Unlock"), false, () =>
                {
                    if (!GitLocksEditor.DisplayForceUnlockConfirmationDialog())
                        return;

                    foreach (var lfsLock in myLocks.Concat(othersLocks))
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
            var index = IdToIndex(id);
            if (index >= _locks.Length)
                return;

            var lfsLock = _locks[index];

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

        private void OnVisibleColumnsChanged(MultiColumnHeader header)
        {
            header.ResizeToFit();
        }
        #endregion
    }
}