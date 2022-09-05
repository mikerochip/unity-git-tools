using UnityEditor.IMGUI.Controls;

namespace GitGoodies.Editor
{
    public class LfsLockColumn
    {
        public LfsLockColumnType Type { get; set; }
        public MultiColumnHeaderState.Column Column { get; set; }
    }
}