// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities;
using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using EvolveOS_ShellEnhancer.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace EvolveOS_ShellEnhancer
{
    public partial class App : Application
    {
        #region Fields & Properties
        private CustomStartMenuWindow? _startMenuWindow;

        private bool _isStartMenuEnabled = false;
        private bool _isTaskbarEnabled = false;

        public static DateTime LastStartMenuCloseTime = DateTime.MinValue;
        #endregion

        #region Initialization
        public App()
        {
            this.InitializeComponent();

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            this.UnhandledException += App_UnhandledException;

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    File.WriteAllText("CrashLog.txt", $"Fatal: {ex.Message}\n{ex.StackTrace}");
                }
            };

            this.UnhandledException += (s, e) =>
            {
                File.WriteAllText("ShellEnhancer_CrashLog_UI.txt", $"Fatal UI: {e.Exception.Message}\n{e.Exception.StackTrace}");
                e.Handled = true;
            };
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            СheckingGlobalParameters.Initialize();

            ApplyFontGlobally(SettingsEngine.Shell_AppFont);

            _startMenuWindow = new CustomStartMenuWindow();

            _startMenuWindow.SetStyle(SettingsEngine.Shell_StartMenuStyle);
            _startMenuWindow.SetAlignment(SettingsEngine.Shell_TaskbarAlignment);
            _startMenuWindow.SetPosition(SettingsEngine.Shell_TaskbarPosition);

            KeyboardHookManager.WindowsKeyPressed += OnWindowsKeyPressed;

            IpcServerManager.CommandReceived += OnIpcCommandReceived;
            IpcServerManager.StartListening();
        }
        #endregion

        #region Lifecycle & Cleanup
        private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            RestoreWindowsDefaults();
            HandleCleanup();
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            RestoreWindowsDefaults();
        }

        private void ApplyFontGlobally(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName))
            {
                fontName = "Segoe UI";
            }

            FontFamily targetFont;
            if (Application.Current.Resources.TryGetValue(fontName, out var resource) && resource is FontFamily customFont)
            {
                targetFont = customFont;
            }
            else
            {
                targetFont = new FontFamily(fontName);
            }

            Application.Current.Resources["AppCustomFont"] = targetFont;
            Application.Current.Resources["ContentControlThemeFontFamily"] = targetFont;

            if (_startMenuWindow != null && _startMenuWindow.Content is FrameworkElement root)
            {
                var currentTheme = root.RequestedTheme;
                var oppositeTheme = root.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;

                root.RequestedTheme = oppositeTheme;
                root.RequestedTheme = currentTheme;
            }

            if (_isTaskbarEnabled)
            {
                TaskbarManager.ReloadAll();
            }
        }

        private void RestoreWindowsDefaults()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
                {
                    if (key != null)
                    {
                        key.SetValue("MMTaskbarEnabled", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        private void HandleCleanup()
        {
            try
            {
                string appName = Assembly.GetExecutingAssembly().GetName().Name ?? "EvolveOS_ShellEnhancer";
                string netTempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", ".net", appName);

                if (Directory.Exists(netTempPath))
                {
                    var directories = Directory.GetDirectories(netTempPath);
                    foreach (var dir in directories)
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App Temp Cleanup Error] {ex.Message}");
            }
        }
        #endregion

        #region Core Functionality
        public void ToggleStartMenu(DisplayArea? displayArea = null, bool forceCustom = false)
        {
            if (!forceCustom && !SettingsEngine.Shell_StartMenuEnabled)
            {
                return;
            }

            if ((_isStartMenuEnabled || forceCustom) && _startMenuWindow != null)
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
            else if (!forceCustom)
            {
                Win32Helper.OpenNativeStartMenu();
            }
        }
        #endregion

        #region IPC Handling
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

                    case "Taskbar_MonitorAware":
                        CustomTaskbarWindow.MonitorAwareApps = bool.Parse(value);
                        TaskbarManager.ReloadAll();
                        break;

                    case "Shell_Font":
                        ApplyFontGlobally(value);
                        break;
                }
            });
        }
        #endregion

        #region Input Handling & Destructor
        private void OnWindowsKeyPressed()
        {
            ToggleStartMenu();
        }

        ~App()
        {
            IpcServerManager.StopListening();
            KeyboardHookManager.StopHook();
        }
        #endregion
    }
}