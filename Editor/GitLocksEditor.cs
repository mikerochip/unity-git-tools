using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace MikeSchweitzer.Git.Editor
{
    public class GitLocksEditor : EditorWindow, IHasCustomMenu
    {
        #region Private Fields
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

        public static bool DisplayForceUnlockConfirmationDialog()
        {
            return EditorUtility.DisplayDialog("Warning",
                "Are you sure you want to force unlock?",
                "OK", "Cancel");
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
            LayoutRefreshControls();

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
            if (GitSettings.Locks.Any())
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

        private void LayoutRefreshControls()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var autoRefreshContent = new GUIContent("Auto Refresh",
                $"Automatically check for Git LFS locks every {GitSettings.UpdateInterval} seconds.");
            GitSettings.ShouldAutoRefreshLocks = EditorGUILayout.ToggleLeft(autoRefreshContent,
                GitSettings.ShouldAutoRefreshLocks,
                GUILayout.Width(100.0f));

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!GitSettings.HasUsername))
            {
                var refreshNowContent = new GUIContent("Force Refresh",
                    "Force a refresh of Git LFS locks now.");
                if (GUILayout.Button(refreshNowContent, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                    GitSettings.ForceRefreshLocks();
            }

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
                "Failed to run Git LFS.\nGit LFS needs to be installed on your machine to manage locks.",
                MessageType.Error);

            LayoutCenteredMessageFooter();
        }

        private void LayoutNoLfsInRepo()
        {
            LayoutCenteredMessageHeader();

            EditorGUILayout.HelpBox(
                "Failed to find [lfs] in Git config.\nGit LFS needs to be installed in this repo to manage locks.",
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

            EditorGUILayout.HelpBox("Refreshing Git LFS Locks...", MessageType.Info);

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
            rect.height = position.height - rect.y;
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
