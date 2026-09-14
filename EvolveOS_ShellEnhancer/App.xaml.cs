// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using EvolveOS_ShellEnhancer.Views;

namespace EvolveOS_ShellEnhancer
{
    public partial class App : Application
    {
        private CustomStartMenuWindow? _startMenuWindow;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _startMenuWindow = new CustomStartMenuWindow();

            KeyboardHookManager.WindowsKeyPressed += OnWindowsKeyPressed;
            KeyboardHookManager.StartHook();

            // Iinitialize invisible Taskbar overlay
            // var taskbarOverlay = new CustomTaskbarWindow();
            // taskbarOverlay.Activate();
        }

        private void OnWindowsKeyPressed()
        {
            _startMenuWindow!.DispatcherQueue.TryEnqueue(() =>
            {
                _startMenuWindow.ToggleVisibility();
            });
        }

        ~App()
        {
            KeyboardHookManager.StopHook();
        }
    }
}