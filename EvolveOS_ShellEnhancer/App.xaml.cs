// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities;
using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using EvolveOS_ShellEnhancer.Views;
using Microsoft.UI.Xaml;
using System;

namespace EvolveOS_ShellEnhancer
{
    public partial class App : Application
    {
        private CustomStartMenuWindow? _startMenuWindow;

        private bool _isStartMenuEnabled = false;
        private bool _isTaskbarEnabled = false;

        public static DateTime LastStartMenuCloseTime = DateTime.MinValue;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            СheckingGlobalParameters.Initialize();

            _startMenuWindow = new CustomStartMenuWindow();

            KeyboardHookManager.WindowsKeyPressed += OnWindowsKeyPressed;

            IpcServerManager.CommandReceived += OnIpcCommandReceived;
            IpcServerManager.StartListening();
        }

        public void ToggleStartMenu(Microsoft.UI.Windowing.DisplayArea? displayArea = null)
        {
            if (_isStartMenuEnabled && _startMenuWindow != null)
            {
                _startMenuWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    if (displayArea != null)
                    {
                        _startMenuWindow.TargetDisplayArea = displayArea;
                    }

                    _startMenuWindow.ToggleVisibility();
                });
            }
            else
            {
                Win32Helper.OpenNativeStartMenu();
            }
        }

        private void OnIpcCommandReceived(string command, string value)
        {
            _startMenuWindow!.DispatcherQueue.TryEnqueue(() =>
            {
                switch (command)
                {
                    case "StartMenu_Enable":
                        _isStartMenuEnabled = bool.Parse(value);
                        if (_isStartMenuEnabled)
                            KeyboardHookManager.StartHook();
                        else
                            KeyboardHookManager.StopHook();
                        break;

                    case "StartMenu_Style":
                        _startMenuWindow.SetStyle(value);
                        break;

                    case "Taskbar_Enable":
                        _isTaskbarEnabled = bool.Parse(value);
                        if (_isTaskbarEnabled)
                        {
                            _ = TaskbarManager.InitializeAndShowTaskbarsAsync();
                        }
                        else
                        {
                            TaskbarManager.HideAll();
                        }
                        break;

                    case "Taskbar_Style":
                        TaskbarManager.SetStyle(value);
                        break;

                    case "Taskbar_Alignment":
                        TaskbarManager.SetAlignment(value);
                        _startMenuWindow?.SetAlignment(value);
                        break;

                    case "Taskbar_Position":
                        TaskbarManager.SetPosition(value);
                        _startMenuWindow?.SetPosition(value);
                        break;

                    case "Taskbar_Animation":
                        CustomTaskbarWindow.PositionAnimationStyle = value;
                        break;

                    case "Taskbar_PreviewButtons":
                        LivePreviewWindow.EnableActionButtons = bool.Parse(value);
                        break;

                    case "Taskbar_ClockSeconds":
                        CustomTaskbarWindow.ShowSeconds = bool.Parse(value);
                        break;

                    case "Taskbar_ShowUnpinned":
                        CustomTaskbarWindow.ShowUnpinnedApps = bool.Parse(value);
                        if (!CustomTaskbarWindow.ShowUnpinnedApps)
                        {
                            TaskbarManager.ResetUnpinnedScrollViewAll();
                        }
                        TaskbarManager.ReloadAll();
                        break;

                    case "Taskbar_UnpinnedMode":
                        CustomTaskbarWindow.UnpinnedDisplayMode = value;
                        TaskbarManager.ReloadAll();
                        break;

                    case "Taskbar_HoverAnimation":
                        CustomTaskbarWindow.HoverAnimationStyle = value;
                        break;

                    case "Taskbar_HoverBackground":
                        CustomTaskbarWindow.ShowHoverBackground = bool.Parse(value);
                        break;
                }
            });
        }

        private void OnWindowsKeyPressed()
        {
            ToggleStartMenu();
        }

        ~App()
        {
            IpcServerManager.StopListening();
            KeyboardHookManager.StopHook();
        }
    }
}