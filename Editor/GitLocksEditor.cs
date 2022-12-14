using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GitTools.Editor
{
    public class GitLocksEditor : EditorWindow, IHasCustomMenu
    {
        #region Private Fields
        private Texture _loadingIcon;
        private MultiColumnHeaderState _multiColumnHeaderState;
        private MultiColumnHeader _multiColumnHeader;
        private TreeViewState _locksTreeViewState = new TreeViewState();
        private GitLocksTreeView _locksTreeView;
        #endregion

        #region Public Methods
        [MenuItem("Window/Git/Locks")]
        public static void ShowWindow()
        {
            var window = GetWindow<GitLocksEditor>("Git Locks", focus: true);
            window.Show();
            GitSettings.RefreshLocks();
        }
        #endregion

        #region Unity Methods
        private void OnEnable()
        {
            InitializeTree();
            
            if (GitSettings.Locks.Any())
                _locksTreeView.Reload();
            
            GitSettings.LocksRefreshed += OnLocksRefreshed;
            GitSettings.LockStatusChanged += OnLockStatusChanged;
        }

        private void OnGUI()
        {
            LayoutHeader();

            if (!GitSettings.IsGitRepo)
                LayoutNonGitRepo();
            else if (!GitSettings.HasLfsProcess)
                LayoutNoLfsProcess();
            else if (!GitSettings.HasLfsInRepo)
                LayoutNoLfsInRepo();
            else if (!GitSettings.HasUsername)
                LayoutUsername();
            else if (!GitSettings.Locks.Any())
                LayoutNoLocks();
            else
                LayoutLocks();
        }
        #endregion

        #region Private Methods
        private void OnLocksRefreshed()
        {
            if (!GitSettings.Locks.Any())
                return;
            
            _locksTreeView.Reload();
            Repaint();
        }

        private void OnLockStatusChanged(LfsLock lfsLock)
        {
            _locksTreeView.Reload();
            Repaint();
        }

        private void InitializeTree()
        {
            var columns = new MultiColumnHeaderState.Column[LfsLockColumnData.Columns.Length];
            var visibleColumns = new List<int>();
            var sortedColumns = new List<int>();
            var sortedColumnIndex = -1;
            for (var i = 0; i < LfsLockColumnData.Columns.Length; ++i)
            {
                var column = LfsLockColumnData.Columns[i].Column;
                columns[i] = column;
                
                if (LfsLockColumnData.Columns[i].IsDefaultVisible)
                    visibleColumns.Add(i);
                
                if (!column.canSort)
                    continue;
                
                sortedColumns.Add(i);
                if (GitSettings.LockSortType == (LfsLockSortType)column.userData)
                    sortedColumnIndex = i;
            }

            _multiColumnHeaderState = new MultiColumnHeaderState(columns)
            {
                sortedColumns = sortedColumns.ToArray(),
                maximumNumberOfSortedColumns = sortedColumns.Count,
                sortedColumnIndex = sortedColumnIndex,
                visibleColumns = visibleColumns.ToArray(),
            };
            _multiColumnHeader = new MultiColumnHeader(_multiColumnHeaderState);
            
            _locksTreeView = new GitLocksTreeView(_locksTreeViewState, _multiColumnHeader);
        }

        private void LayoutHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                
            using (new EditorGUI.DisabledScope(!GitSettings.HasUsername))
            {
                EditorGUILayout.LabelField("Locks", EditorStyles.boldLabel, GUILayout.MaxWidth(150.0f));
                
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                    GitSettings.ForceRefreshLocks();
            }
                
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void LayoutNonGitRepo()
        {
            LayoutCenteredMessageHeader();
            
            EditorGUILayout.HelpBox(
                "This is not in a Git repo.",
                MessageType.Error);
            
            LayoutCenteredMessageFooter();
        }

        private void LayoutNoLfsProcess()
        {
            LayoutCenteredMessageHeader();
            
            EditorGUILayout.HelpBox(
                "Failed to run Git LFS.\nYou need LFS to manage locks.",
                MessageType.Error);
            
            LayoutCenteredMessageFooter();
        }

        private void LayoutNoLfsInRepo()
        {
            LayoutCenteredMessageHeader();
            
            EditorGUILayout.HelpBox(
                "Failed to find [lfs] in Git config.\nYou need LFS to manage locks.",
                MessageType.Warning);
            
            LayoutCenteredMessageFooter();
        }

        private void LayoutUsername()
        {
            LayoutCenteredMessageHeader();
            
            EditorGUILayout.LabelField("Enter Git Username");
            EditorGUILayout.Space();
            GitSettings.Username = EditorGUILayout.DelayedTextField(GitSettings.Username);
            
            LayoutCenteredMessageFooter();
        }
        
        private void LayoutNoLocks()
        {
            if (GitSettings.AreLocksRefreshing)
                LayoutRefreshingLocks();
            else
                LayoutNothingLocked();
        }
        
        private void LayoutRefreshingLocks()
        {
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox("Refreshing Locks...", MessageType.Info);
            
            EditorGUILayout.Space();
        }

        private void LayoutNothingLocked()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            var content = new GUIContent("Nothing is locked");
            var contentWidth = 0.5f * (position.width - EditorStyles.label.CalcSize(content).x);
            var spacerWidth = Mathf.Max(contentWidth, 0.0f);
            GUILayout.Space(spacerWidth);
            EditorGUILayout.LabelField(content);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
        }
        
        private void LayoutLocks()
        {
            var rect = GUILayoutUtility.GetLastRect();
            rect.y += rect.height;
            rect.width = position.width;
            rect.height = position.height;
            _locksTreeView.OnGUI(rect);
        }

        private void LayoutCenteredMessageHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
        }
        
        private void LayoutCenteredMessageFooter()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        #endregion

        #region IHasCustomMenu Methods
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Reset Git Username"), false, () =>
            {
                GitSettings.Username = null;
            });
            menu.AddItem(new GUIContent("Show Plain Text Paths"), GitLocksEditorSettings.IsShowingPlainTextPaths, () =>
            {
                GitLocksEditorSettings.IsShowingPlainTextPaths = !GitLocksEditorSettings.IsShowingPlainTextPaths;
            });
        }
        #endregion
    }
}