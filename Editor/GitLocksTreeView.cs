using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

using Object = UnityEngine.Object;

namespace GitGoodies.Editor
{
    public class GitLocksTreeView : TreeView
    {
        private LfsLock[] _locks;
        
        public GitLocksTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader)
            : base(state, multiColumnHeader)
        {
            useScrollView = true;
            multiColumnHeader.ResizeToFit();
            multiColumnHeader.sortingChanged += MultiColumnHeaderOnSortingChanged;
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

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            
            var lfsLock = _locks[id];
            var hasLock = GitSettings.Username == lfsLock.User;
            
            var menu = new GenericMenu();
            
            if (hasLock)
            {
                menu.AddItem(new GUIContent("Unlock"), false, () =>
                    GitSettings.Unlock(lfsLock.Id));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Unlock"), false);
            }
            menu.AddItem(new GUIContent("Force Unlock"), false, () =>
                GitSettings.ForceUnlock(lfsLock.Id));
            
            menu.AddSeparator("");
            menu.AddItem(new GUIContent($"Copy/User"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock.User);
            menu.AddItem(new GUIContent($"Copy/Path"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock.Path);
            menu.AddItem(new GUIContent($"Copy/Asset GUID"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock.AssetGuid);
            menu.AddItem(new GUIContent($"Copy/Lock ID"), false, () => 
                GUIUtility.systemCopyBuffer = lfsLock.Id);
            
            menu.ShowAsContext();
            
            Event.current.Use();
        }

        private void MultiColumnHeaderOnSortingChanged(MultiColumnHeader _)
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
    }
}