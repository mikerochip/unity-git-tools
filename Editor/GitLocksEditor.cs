using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace GitGoodies.Editor
{
    public class GitLocksEditor : EditorWindow, IHasCustomMenu
    {
        #region Private Fields
        private Vector2 _scrollPos;
        private GUILayoutOption _singleLineHeight;
        private bool _showIds;
        #endregion

        #region Private Properties
        private static bool CanShowLocks => GitSettings.HasUsername;
        #endregion

        #region Public Methods
        [MenuItem("Window/Git Goodies/Locks")]
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
            GitSettings.LocksRefreshed += OnLocksRefreshed;
        }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(!GitSettings.HasUsername))
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                EditorGUILayout.LabelField("Locks", EditorStyles.boldLabel, GUILayout.MaxWidth(150.0f));
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                    GitSettings.RefreshLocks();
                _showIds = EditorGUILayout.ToggleLeft("Show Lock ID", _showIds, GUILayout.MaxWidth(100.0f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            _singleLineHeight = GUILayout.Height(EditorGUIUtility.singleLineHeight);

            if (CanShowLocks)
                LayoutLocks();
            else
                LayoutUsername();
            
            EditorGUILayout.EndScrollView();
        }
        #endregion

        #region Private Methods
        private void OnLocksRefreshed(object sender, EventArgs e)
        {
            Repaint();
        }

        private void LayoutUsername()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Enter your GitHub Username");
            EditorGUILayout.Space();
            GitSettings.Username = EditorGUILayout.DelayedTextField(GitSettings.Username);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        private void LayoutLocks()
        {
            if (!GitSettings.Locks.Any())
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
                return;
            }
            
            foreach (var lfsLock in GitSettings.Locks)
            {
                EditorGUILayout.Space();
                
                EditorGUILayout.BeginHorizontal();

                LayoutButtons(lfsLock);
                LayoutStats(lfsLock);
                
                EditorGUILayout.EndHorizontal();
            }

            void LayoutButtons(LfsLock lfsLock)
            {
                const float buttonWidth = 50.0f;

                var hasLock = lfsLock.User == GitSettings.Username;
                
                var paddingHeight = _showIds
                    ? 0.5f * EditorGUIUtility.singleLineHeight
                    : 0.0f;
                
                EditorGUILayout.BeginVertical(GUILayout.MaxWidth(buttonWidth));
                GUILayoutUtility.GetRect(0.0f, paddingHeight);
                
                using (new EditorGUI.DisabledScope(!hasLock))
                {
                    var tooltip = hasLock ? "" : "This is locked by someone else";
                    if (GUILayout.Button(new GUIContent("Unlock", tooltip), GUILayout.Width(buttonWidth)))
                        GitSettings.Unlock(lfsLock.Id);
                }
                
                const string forceTooltip = "Unlock someone else's lock";
                if (GUILayout.Button(new GUIContent("FUnlock", forceTooltip), GUILayout.Width(buttonWidth)))
                    GitSettings.ForceUnlock(lfsLock.Id);
                
                GUILayoutUtility.GetRect(0.0f, paddingHeight);
                EditorGUILayout.EndVertical();
            }

            void LayoutStats(LfsLock lfsLock)
            {
                EditorGUILayout.BeginVertical();
                
                var asset = AssetDatabase.LoadAssetAtPath<Object>(lfsLock.Path);
                if (asset == null)
                    EditorGUILayout.SelectableLabel(lfsLock.Path, _singleLineHeight);
                else
                    EditorGUILayout.ObjectField(asset, asset.GetType(), allowSceneObjects: false);
                EditorGUILayout.SelectableLabel($"Locked by {lfsLock.User}", _singleLineHeight);
                if (_showIds)
                    EditorGUILayout.SelectableLabel($"ID {lfsLock.Id}", _singleLineHeight);
                
                EditorGUILayout.EndVertical();
            }
        }
        #endregion

        #region IHasCustomMenu Methods
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Reset GitHub Username"), false, () =>
            {
                GitSettings.Username = null;
            });
        }
        #endregion
    }
}