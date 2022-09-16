using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GitGoodies.Editor
{
    [InitializeOnLoad]
    public class GitProjectWindowHelper
    {
        #region Private Fields
        private static Texture _loadingIcon;
        private static Color _loadingColor;
        private static Texture _lockIcon;
        private static Color _lockColor;
        #endregion

        #region Public Methods
        static GitProjectWindowHelper()
        {
            Initialize();
            
            EditorApplication.projectWindowItemOnGUI += ProjectWindowItemOnGUI;
            GitSettings.LocksRefreshed += OnLocksRefreshed;
            GitSettings.LockStatusChanged += OnLockStatusChanged;
        }
        #endregion

        #region Private Methods
        private static void Initialize()
        {
            _loadingIcon = EditorGUIUtility.FindTexture("d_WaitSpin00");
            _loadingColor = new Color(1.0f, 1.0f, 1.0f);
            _lockIcon = EditorGUIUtility.FindTexture("d_AssemblyLock");
            _lockColor = new Color(1.0f, 0.5f, 0.1f);
        }
        
        private static void ProjectWindowItemOnGUI(string guid, Rect selectionRect)
        {
            var lfsLock = GitSettings.Locks.FirstOrDefault(lfsLock => lfsLock.AssetGuid == guid);
            if (lfsLock == null)
                return;

            var icon = lfsLock.IsPending ? _loadingIcon : _lockIcon;
            var color = lfsLock.IsPending ? _loadingColor : _lockColor;
            
            var rect = selectionRect;
            rect.x = selectionRect.xMax - icon.width;
            rect.width += icon.width;

            var hasLock = lfsLock.User == GitSettings.Username;
            var tooltip = hasLock ? "Locked by you" : $"Locked by {lfsLock.User}";
            if (!GitSettings.HasUsername)
                tooltip += "\n\nTo use locks, set your Git username in preferences";
            
            var prevColor = GUI.contentColor;
            GUI.contentColor = color;
            EditorGUI.LabelField(rect, new GUIContent(icon, tooltip));
            GUI.contentColor = prevColor;
        }

        private static void OnLocksRefreshed()
        {
            EditorApplication.RepaintProjectWindow();
        }

        private static void OnLockStatusChanged(LfsLock lfsLock)
        {
            EditorApplication.RepaintProjectWindow();
        }
        #endregion
    }
}