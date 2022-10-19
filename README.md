# Unity Git Tools

Improve your Unity project's Git integration with this package. Mostly helps you manage Git LFS Locks in the editor.

# How to Use

* Set your username for your hosting service (GitHub, GitLab, etc) in ```Preferences > Git```
* Open the Git LFS Locks UI with ```Window > Git > Locks```
* Right click on an asset in the Project tab to lock, unlock, or force unlock it

# Samples

<img src="https://user-images.githubusercontent.com/498714/196613042-7129ce08-32fb-4dee-9c1e-33c63561868b.gif" width=568>

Locks Window

| Without Lock ID | With Lock ID |
|-----------------|--------------|
| <img src="https://user-images.githubusercontent.com/498714/196605643-c003740f-99b5-4ea7-ab47-edd459f53fe0.png" width=475> | <img src="https://user-images.githubusercontent.com/498714/196605680-251905e1-8c3c-4bc9-a93b-f10a30e9fe2c.png" width=475> |

Raw output from command line

<img src="https://user-images.githubusercontent.com/498714/196608385-77d5cbcd-c7f8-4e20-b862-355be3acd418.png" width=500>

Branch name can be accessed with ```GitSettings.Branch```

Showing it inside the toolbar is possible with the awesome [unity-toolbar-extender](https://github.com/marijnz/unity-toolbar-extender):

```C#
using GitTools.Editor;
using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;

namespace Editor
{
    [InitializeOnLoad]
    public static class Toolbar
    {
        static Toolbar()
        {
            var branchStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState
                {
                    textColor = new Color(0.1f, 0.8f, 1.0f),
                },
            };

            ToolbarExtender.LeftToolbarGUI.Add(() =>
            {
                GUILayout.Space(100.0f);
                EditorGUILayout.LabelField($"Branch: {GitSettings.Branch}", branchStyle);
                GUILayout.FlexibleSpace();
            });
        }
    }
}

```
