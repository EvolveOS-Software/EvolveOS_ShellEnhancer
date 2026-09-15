// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using EvolveOS_ShellEnhancer.Views;
using Microsoft.UI.Xaml;

namespace EvolveOS_ShellEnhancer
{
    public partial class App : Application
    {
        private CustomStartMenuWindow? _startMenuWindow;
        private CustomTaskbarWindow? _taskbarWindow;

        private bool _isStartMenuEnabled = false;
        private bool _isTaskbarEnabled = false;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _startMenuWindow = new CustomStartMenuWindow();
            _taskbarWindow = new CustomTaskbarWindow();

            KeyboardHookManager.WindowsKeyPressed += OnWindowsKeyPressed;

            IpcServerManager.CommandReceived += OnIpcCommandReceived;
            IpcServerManager.StartListening();
        }

        public void ToggleStartMenu()
        {
            if (_isStartMenuEnabled && _startMenuWindow != null)
            {
                _startMenuWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    _startMenuWindow.ToggleVisibility();
                });
            }
            else
            {
                EvolveOS_ShellEnhancer.Utilities.Helpers.Win32Helper.OpenNativeStartMenu();
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
                            _taskbarWindow?.ShowDock();
                        else
                            _taskbarWindow?.HideDock();
                        break;

                    case "Taskbar_Style":
                        _taskbarWindow?.SetStyle(value);
                        break;

                    case "Taskbar_Alignment":
                        _taskbarWindow?.SetAlignment(value);
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