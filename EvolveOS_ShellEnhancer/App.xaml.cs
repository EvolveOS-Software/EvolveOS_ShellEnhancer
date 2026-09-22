// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Windowing;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace EvolveOS_ShellEnhancer
{
    public partial class App : Application
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

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
            if (!RunGuard.ValidateStartupEnvironment())
            {
                return;
            }

            СheckingGlobalParameters.Initialize();

            string savedLang = SettingsEngine.Shell_Language;
            if (!string.IsNullOrEmpty(savedLang))
            {
                LocalizationService.Instance.SetLanguage(savedLang);
            }

            try
            {
                var process = Process.GetCurrentProcess();
                process.PriorityBoostEnabled = true;
                process.PriorityClass = SettingsEngine.Shell_HighPriority ? ProcessPriorityClass.High : ProcessPriorityClass.Normal;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set initial process priority: {ex.Message}");
            }

            ApplyFontGlobally(SettingsEngine.Shell_AppFont);
            ApplyFontSizeGlobally(SettingsEngine.Shell_AppFontSize);

            try
            {
                CustomTaskbarWindow.TaskbarSize = SettingsEngine.Shell_TaskbarSize;
                CustomTaskbarWindow.TaskbarIconSize = SettingsEngine.Shell_TaskbarIconSize;
                CustomTaskbarWindow.TaskbarLength = SettingsEngine.Shell_TaskbarLength;
                CustomTaskbarWindow.TaskbarCornerRadius = SettingsEngine.Shell_TaskbarCornerRadius;
                CustomTaskbarWindow.PreviewDelay = SettingsEngine.Shell_TaskbarPreviewDelay;

                LivePreviewWindow.EnableActionButtons = SettingsEngine.Shell_TaskbarPreviewButtons;
                LivePreviewWindow.EnableAnimations = SettingsEngine.Shell_TaskbarPreviewAnimation;
                LivePreviewWindow.AnimationStyle = SettingsEngine.Shell_TaskbarPreviewAnimStyle ?? "Standard";
                LivePreviewWindow.AnimationSpeed = SettingsEngine.Shell_TaskbarPreviewAnimSpeed;

                CustomTaskbarWindow.ShowSeconds = SettingsEngine.Shell_TaskbarClockSeconds;
                CustomTaskbarWindow.ShowUnpinnedApps = SettingsEngine.Shell_TaskbarShowUnpinned;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Self-Start] Failed to pre-load static settings: {ex.Message}");
            }

            CustomStartMenuWindow.ShowPowerSleep = SettingsEngine.Shell_StartMenuPowerSleep;
            CustomStartMenuWindow.ShowPowerRestartBios = SettingsEngine.Shell_StartMenuPowerRestartBios;
            CustomStartMenuWindow.ShowPowerLogOff = SettingsEngine.Shell_StartMenuPowerLogOff;
            CustomStartMenuWindow.EnableAnimations = SettingsEngine.Shell_StartMenuAnimation;
            CustomStartMenuWindow.AnimationStyle = SettingsEngine.Shell_StartMenuAnimStyle ?? "Standard";
            CustomStartMenuWindow.AnimationSpeed = SettingsEngine.Shell_StartMenuAnimSpeed;
            CustomStartMenuWindow.ShowRecentDocs = SettingsEngine.Shell_StartMenuRecentDocs;

            _startMenuWindow = new CustomStartMenuWindow();

            _startMenuWindow.SetStyle(SettingsEngine.Shell_StartMenuStyle);
            _startMenuWindow.SetAlignment(SettingsEngine.Shell_TaskbarAlignment);
            _startMenuWindow.SetPosition(SettingsEngine.Shell_TaskbarPosition);

            TaskbarManager.SetStyle(SettingsEngine.Shell_TaskbarStyle ?? "Standard");
            TaskbarManager.SetAlignment(SettingsEngine.Shell_TaskbarAlignment ?? "Center");
            TaskbarManager.SetPosition(SettingsEngine.Shell_TaskbarPosition ?? "Bottom");

            CustomTaskbarWindow.PositionAnimationStyle = SettingsEngine.Shell_TaskbarAnimation ?? "Spring";
            CustomTaskbarWindow.HoverAnimationStyle = SettingsEngine.Shell_TaskbarHoverAnimation ?? "Standard";
            CustomTaskbarWindow.ShowHoverBackground = SettingsEngine.Shell_TaskbarHoverBackground;
            CustomTaskbarWindow.MonitorAwareApps = SettingsEngine.Shell_TaskbarMonitorAware;
            CustomTaskbarWindow.UnpinnedDisplayMode = SettingsEngine.Shell_TaskbarUnpinnedMode ?? "Inline";

            KeyboardHookManager.WindowsKeyPressed += OnWindowsKeyPressed;

            try
            {
                bool isMasterEnabled = SettingsEngine.Shell_MasterEnabled;
                if (isMasterEnabled)
                {
                    _ = WaitForExplorerAndStartAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Self-Start] Failed to initialize overlays on boot: {ex.Message}");
            }

            IpcServerManager.CommandReceived += OnIpcCommandReceived;
            IpcServerManager.StartListening();
        }

        private async Task WaitForExplorerAndStartAsync()
        {
            int retries = 0;
            while (FindWindow("Shell_TrayWnd", null) == IntPtr.Zero && retries < 20)
            {
                await Task.Delay(500);
                retries++;
            }

            _isTaskbarEnabled = SettingsEngine.Shell_TaskbarEnabled;
            if (_isTaskbarEnabled)
            {
                _ = TaskbarManager.InitializeAndShowTaskbarsAsync();
            }

            _isStartMenuEnabled = SettingsEngine.Shell_StartMenuEnabled;
            if (_isStartMenuEnabled)
            {
                KeyboardHookManager.StartHook();
            }
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

            if (_startMenuWindow != null && _startMenuWindow.Content is FrameworkElement root && _startMenuWindow.Visible)
            {
                var currentTheme = root.RequestedTheme;
                var oppositeTheme = root.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;

                root.RequestedTheme = oppositeTheme;

                _startMenuWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    root.RequestedTheme = currentTheme;
                });
            }

            if (_isTaskbarEnabled)
            {
                TaskbarManager.ReloadAll();
            }
        }

        private void ApplyFontSizeGlobally(double baseSize)
        {
            if (baseSize <= 0) baseSize = 14.0;

            Application.Current.Resources["AppFontSizeBase"] = baseSize;
            Application.Current.Resources["AppFontSizeSmall"] = Math.Max(baseSize - 2, 9.0);   // E.g., 12 (Clamped so it never goes below 9)
            Application.Current.Resources["AppFontSizeTiny"] = Math.Max(baseSize - 4, 8.0);    // E.g., 10
            Application.Current.Resources["AppFontSizeHeader"] = baseSize + 2;                 // E.g., 18
            Application.Current.Resources["AppFontSizeTitle"] = baseSize + 8;                  // E.g., 24

            Application.Current.Resources["ControlContentThemeFontSize"] = baseSize;
            Application.Current.Resources["BodyTextBlockFontSize"] = baseSize;

            RefreshUITheme();
        }

        private void RefreshUITheme()
        {
            if (_startMenuWindow != null && _startMenuWindow.Content is FrameworkElement root && _startMenuWindow.Visible)
            {
                var currentTheme = root.RequestedTheme;
                var oppositeTheme = root.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;

                root.RequestedTheme = oppositeTheme;

                _startMenuWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    root.RequestedTheme = currentTheme;
                });
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

        public static void ExitApp()
        {
            HandleCleanup();

            Application.Current.Exit();
        }

        private static void HandleCleanup()
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
            if (_startMenuWindow == null || _startMenuWindow.DispatcherQueue == null)
            {
                Debug.WriteLine($"[IPC] Dropped command {command} - Window not ready.");
                return;
            }

            _startMenuWindow.DispatcherQueue.TryEnqueue(() =>
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

                    case "StartMenu_Animation":
                        CustomStartMenuWindow.EnableAnimations = bool.Parse(value);
                        break;

                    case "StartMenu_AnimStyle":
                        CustomStartMenuWindow.AnimationStyle = value;
                        break;

                    case "StartMenu_AnimSpeed":
                        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double smSpeed))
                            CustomStartMenuWindow.AnimationSpeed = smSpeed;
                        break;

                    case "StartMenu_ProfileClick":
                        SettingsEngine.Shell_StartMenuProfileClick = bool.Parse(value);
                        break;

                    case "StartMenu_PowerRestartBios":
                        SettingsEngine.Shell_StartMenuPowerRestartBios = bool.Parse(value);
                        CustomStartMenuWindow.ShowPowerRestartBios = SettingsEngine.Shell_StartMenuPowerRestartBios;
                        break;

                    case "StartMenu_PowerLogOff":
                        SettingsEngine.Shell_StartMenuPowerLogOff = bool.Parse(value);
                        CustomStartMenuWindow.ShowPowerLogOff = SettingsEngine.Shell_StartMenuPowerLogOff;
                        break;

                    case "StartMenu_PowerSleep":
                        SettingsEngine.Shell_StartMenuPowerSleep = bool.Parse(value);
                        CustomStartMenuWindow.ShowPowerSleep = SettingsEngine.Shell_StartMenuPowerSleep;
                        break;

                    case "StartMenu_RecentDocs":
                        SettingsEngine.Shell_StartMenuRecentDocs = bool.Parse(value);
                        CustomStartMenuWindow.ShowRecentDocs = SettingsEngine.Shell_StartMenuRecentDocs;

                        _startMenuWindow?.LoadRecentDocuments();
                        break;

                    case "StartMenu_Shortcuts":
                        SettingsEngine.Shell_StartMenuShortcuts = value;
                        _startMenuWindow?.UpdateShortcuts(value);
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

                    case "Taskbar_Size":
                        if (int.TryParse(value, out int tSize))
                        {
                            CustomTaskbarWindow.TaskbarSize = tSize;
                            TaskbarManager.ResizeAll();
                        }
                        break;

                    case "Taskbar_Length":
                        if (int.TryParse(value, out int length))
                        {
                            CustomTaskbarWindow.TaskbarLength = length;
                            TaskbarManager.ResizeAll();
                        }
                        break;

                    case "Taskbar_CornerRadius":
                        if (int.TryParse(value, out int radius))
                        {
                            CustomTaskbarWindow.TaskbarCornerRadius = radius;
                            TaskbarManager.ReloadAll();
                        }
                        break;

                    case "Taskbar_IconSize":
                        if (int.TryParse(value, out int iSize))
                        {
                            CustomTaskbarWindow.TaskbarIconSize = iSize;
                            TaskbarManager.ReloadAll();
                        }
                        break;

                    case "Taskbar_Animation":
                        CustomTaskbarWindow.PositionAnimationStyle = value;
                        break;

                    case "Taskbar_PreviewButtons":
                        LivePreviewWindow.EnableActionButtons = bool.Parse(value);
                        break;

                    case "Taskbar_PreviewAnimation":
                        LivePreviewWindow.EnableAnimations = bool.Parse(value);
                        break;

                    case "Taskbar_PreviewAnimStyle":
                        LivePreviewWindow.AnimationStyle = value;
                        break;

                    case "Taskbar_PreviewAnimSpeed":
                        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double pSpeed))
                            LivePreviewWindow.AnimationSpeed = pSpeed;
                        break;

                    case "Taskbar_PreviewDelay":
                        if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double pDelay))
                            CustomTaskbarWindow.PreviewDelay = pDelay;
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

                    case "Taskbar_PinItem":
                        try
                        {
                            string decodedPath = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
                            CustomTaskbarWindow.PinItemToTaskbar(decodedPath);
                        }
                        catch
                        {
                            CustomTaskbarWindow.PinItemToTaskbar(value);
                        }
                        break;

                    case "Taskbar_FolderSubmenus":
                        if (bool.TryParse(value, out bool showSubmenus))
                        {
                            SettingsEngine.Taskbar_ShowFoldersAsSubmenus = showSubmenus;
                            TaskbarManager.ReloadAll();
                        }
                        break;

                    case "Taskbar_FilteredFolders":
                        try
                        {
                            string decodedPaths = Encoding.UTF8.GetString(Convert.FromBase64String(value));
                            SettingsEngine.Taskbar_FilteredFolders = decodedPaths;
                        }
                        catch
                        {
                            SettingsEngine.Taskbar_FilteredFolders = value;
                        }
                        TaskbarManager.ReloadAll();
                        break;

                    case "Shell_Font":
                        ApplyFontGlobally(value);
                        break;

                    case "Shell_FontSize":
                        if (double.TryParse(value, out double size))
                        {
                            ApplyFontSizeGlobally(size);
                        }
                        break;

                    case "Shell_Language":
                        LocalizationService.Instance.SetLanguage(value);
                        break;

                    case "Shell_HighPriority":
                        if (bool.TryParse(value, out bool highPriority))
                        {
                            try
                            {
                                var process = Process.GetCurrentProcess();
                                process.PriorityBoostEnabled = true;
                                process.PriorityClass = highPriority ? ProcessPriorityClass.High : ProcessPriorityClass.Normal;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to set process priority: {ex.Message}");
                            }
                        }
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