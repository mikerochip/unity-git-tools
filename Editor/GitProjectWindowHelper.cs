using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GitGoodies.Editor
{
    [InitializeOnLoad]
    public class GitProjectWindowHelper
    {
        #region Private Fields
        private static Texture _lockIcon;
        private static Color _lockColor;
        #endregion

        #region Public Methods
        static GitProjectWindowHelper()
        {
            Initialize();
            EditorApplication.projectWindowItemOnGUI += ProjectWindowItemOnGUI;
            GitSettings.LocksRefreshed += OnLocksRefreshed;
        }
        #endregion

        #region Private Methods
        private static void Initialize()
        {
            _lockIcon = EditorGUIUtility.FindTexture("d_AssemblyLock");
            _lockColor = new Color(1.0f, 0.5f, 0.1f);
        }
        
        private static void ProjectWindowItemOnGUI(string guid, Rect selectionRect)
        {
            var lfsLock = GitSettings.Locks.FirstOrDefault(lfsLock => lfsLock.AssetGuid == guid);
            if (lfsLock == null)
                return;
            
            var rect = selectionRect;
            rect.x = selectionRect.xMax - _lockIcon.width;
            rect.width += _lockIcon.width;

            var hasLock = lfsLock.User == GitSettings.Username;
            var tooltip = hasLock ? "Locked by you" : $"Locked by {lfsLock.User}";
            if (!GitSettings.HasUsername)
                tooltip += "\n\nTo use locks, set your Git username in preferences";
            
            var prevColor = GUI.contentColor;
            GUI.contentColor = _lockColor;
            EditorGUI.LabelField(rect, new GUIContent(_lockIcon, tooltip));
            GUI.contentColor = prevColor;
        }

        private static void OnLocksRefreshed(object sender, EventArgs e)
        {
            EditorApplication.RepaintProjectWindow();
        }
        #endregion
    }
}