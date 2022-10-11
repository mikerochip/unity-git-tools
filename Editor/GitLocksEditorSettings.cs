using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GitTools.Editor
{
    [FilePath("UserSettings/Git/LocksEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class GitLocksEditorSettings : ScriptableSingleton<GitLocksEditorSettings>
    {
        #region Public Properties
        public static bool IsShowingPlainTextPaths
        {
            get => instance._ShowingPlainTextPaths;
            set
            {
                instance._ShowingPlainTextPaths = value;
                Save();
            }
        }
        #endregion
        
        #region Private Fields
        [SerializeField] private bool _ShowingPlainTextPaths;
        #endregion
        
        #region Helpers
        private static void Save()
        {
            instance.Save(saveAsText: true);
        }
        #endregion
    }
}
