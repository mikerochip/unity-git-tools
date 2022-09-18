using UnityEditor.IMGUI.Controls;

namespace GitTools.Editor
{
    public class LfsLockColumn
    {
        public LfsLockColumnType Type { get; set; }
        public bool IsDefaultVisible { get; set; } = true;
        public MultiColumnHeaderState.Column Column { get; set; }
    }
}