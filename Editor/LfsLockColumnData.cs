using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GitGoodies.Editor
{
    public static class LfsLockColumnData
    {
        public static LfsLockColumn[] Columns { get; } =
        {
            new LfsLockColumn
            {
                Type = LfsLockColumnType.User,
                Column = new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("User"),
                    userData = (int)LfsLockSortType.User,
                    canSort = true,
                    allowToggleVisibility = false,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right,
                    minWidth = 60.0f,
                    width = 60.0f,
                }
            },
            new LfsLockColumn
            {
                Type = LfsLockColumnType.Path,
                Column = new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Asset Path"),
                    userData = (int)LfsLockSortType.Path,
                    canSort = true,
                    allowToggleVisibility = false,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right,
                    minWidth = 150.0f,
                    width = 150.0f,
                },
            },
            new LfsLockColumn
            {
                Type = LfsLockColumnType.Id,
                IsDefaultVisible = false,
                Column = new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Lock ID"),
                    userData = (int)LfsLockSortType.Id,
                    canSort = true,
                    allowToggleVisibility = true,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right,
                    minWidth = 60.0f,
                    width = 60.0f,
                },
            },
        };
        
    }
}