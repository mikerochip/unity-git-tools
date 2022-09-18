using UnityEditor;
using UnityEngine;

namespace GitTools.Editor
{
    public static class GitSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Git", SettingsScope.User)
            {
                label = "Git",
                guiHandler = OnGUI,
            };
            return provider;
        }

        private static void OnGUI(string searchContext)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(7.0f);
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.LabelField("Enter your Git Username");
            EditorGUILayout.Space();
            GitSettings.Username = EditorGUILayout.DelayedTextField(GitSettings.Username);

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}