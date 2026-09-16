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

        public static string CurrentPositionRaw = "Bottom";

        public static async Task InitializeAndShowTaskbarsAsync()
        {
            try
            {
                CurrentAlignment = SettingsEngine.Shell_TaskbarAlignment;
                CurrentPositionRaw = SettingsEngine.Shell_TaskbarPosition;

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

        public static string GetPositionForDisplay(string displayId)
        {
            if (string.IsNullOrWhiteSpace(CurrentPositionRaw)) return "Bottom";

            if (!CurrentPositionRaw.Contains(":"))
            {
                if (CurrentPositionRaw.Contains("Top", StringComparison.OrdinalIgnoreCase)) return "Top";
                if (CurrentPositionRaw.Contains("Left", StringComparison.OrdinalIgnoreCase)) return "Left";
                if (CurrentPositionRaw.Contains("Right", StringComparison.OrdinalIgnoreCase)) return "Right";
                return "Bottom";
            }

            foreach (var part in CurrentPositionRaw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && kv[0] == displayId)
                {
                    string pos = kv[1];
                    if (pos.Contains("Top", StringComparison.OrdinalIgnoreCase)) return "Top";
                    if (pos.Contains("Left", StringComparison.OrdinalIgnoreCase)) return "Left";
                    if (pos.Contains("Right", StringComparison.OrdinalIgnoreCase)) return "Right";
                    return "Bottom";
                }
            }

            return "Bottom";
        }

        private static void ApplyCurrentSettings(CustomTaskbarWindow tb)
        {
            tb.SetStyle(CurrentStyle);

            string displayId = tb.MonitorArea.DisplayId.Value.ToString();
            tb.SetPosition(GetPositionForDisplay(displayId));
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

        public static void SetPosition(string positionString)
        {
            CurrentPositionRaw = positionString ?? "Bottom";
            foreach (var t in _taskbars)
            {
                string displayId = t.MonitorArea.DisplayId.Value.ToString();
                t.SetPosition(GetPositionForDisplay(displayId));
            }
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