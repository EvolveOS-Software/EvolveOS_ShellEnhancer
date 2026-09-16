// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Views;
using Microsoft.UI.Windowing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace EvolveOS_ShellEnhancer.Utilities.Managers
{
    public static class TaskbarManager
    {
        private static readonly List<CustomTaskbarWindow> _taskbars = new();

        public static string CurrentStyle = "Standard";
        public static string CurrentAlignment = "Center";
        public static string CurrentPosition = "Bottom";

        public static async Task InitializeAndShowTaskbarsAsync()
        {
            try
            {
                CurrentAlignment = SettingsEngine.Shell_TaskbarAlignment;
                CurrentPosition = SettingsEngine.Shell_TaskbarPosition;

                if (_taskbars.Count == 0)
                {
                    var displays = DisplayArea.FindAll();

                    if (displays == null || displays.Count == 0)
                    {
                        var tb = new CustomTaskbarWindow();
                        ApplyCurrentSettings(tb);
                        _taskbars.Add(tb);
                    }
                    else
                    {
                        for (int i = 0; i < displays.Count; i++)
                        {
                            var display = displays[i];
                            var tb = new CustomTaskbarWindow(display);
                            ApplyCurrentSettings(tb);
                            _taskbars.Add(tb);
                            await Task.Delay(50);
                        }
                    }
                }

                for (int i = 0; i < _taskbars.Count; i++)
                {
                    _taskbars[i].ShowDock();
                    await Task.Delay(50);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Multi-monitor init failed: {ex.Message}");

                if (_taskbars.Count == 0)
                {
                    var tb = new CustomTaskbarWindow();
                    ApplyCurrentSettings(tb);
                    _taskbars.Add(tb);
                    tb.ShowDock();
                }
            }
        }

        private static void ApplyCurrentSettings(CustomTaskbarWindow tb)
        {
            tb.SetStyle(CurrentStyle);
            tb.SetPosition(CurrentPosition);
        }

        public static void ShowAll()
        {
            _ = InitializeAndShowTaskbarsAsync();
        }

        public static void HideAll()
        {
            foreach (var t in _taskbars) t.HideDock();
        }

        public static void SetStyle(string style)
        {
            CurrentStyle = style;
            foreach (var t in _taskbars) t.SetStyle(style);
        }

        public static void SetAlignment(string alignment)
        {
            if (string.IsNullOrWhiteSpace(alignment)) alignment = "Center";
            else if (alignment.Contains("Left", StringComparison.OrdinalIgnoreCase)) alignment = "Left";
            else if (alignment.Contains("Split", StringComparison.OrdinalIgnoreCase)) alignment = "Split";
            else alignment = "Center";

            CurrentAlignment = alignment;
            foreach (var t in _taskbars) t.SetAlignment(alignment);
        }

        public static void SetPosition(string position)
        {
            if (string.IsNullOrWhiteSpace(position)) position = "Bottom";
            else if (position.Contains("Top", StringComparison.OrdinalIgnoreCase)) position = "Top";
            else if (position.Contains("Left", StringComparison.OrdinalIgnoreCase)) position = "Left";
            else if (position.Contains("Right", StringComparison.OrdinalIgnoreCase)) position = "Right";
            else position = "Bottom";

            CurrentPosition = position;
            foreach (var t in _taskbars) t.SetPosition(position);
        }

        public static void ReloadAll()
        {
            foreach (var t in _taskbars) t.ReloadTaskbar();
        }

        public static void ResetUnpinnedScrollViewAll()
        {
            foreach (var t in _taskbars) t.ResetUnpinnedScrollView();
        }
    }
}