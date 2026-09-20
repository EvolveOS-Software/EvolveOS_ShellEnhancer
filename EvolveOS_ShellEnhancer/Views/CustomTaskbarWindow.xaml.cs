// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Models;
using EvolveOS_ShellEnhancer.Utilities.Animations;
using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Win32;
using Microsoft.Windows.System.Power;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Networking.Connectivity;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using WinRT.Interop;
using static EvolveOS_ShellEnhancer.Utilities.Managers.TaskbarOverlayManager;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class CustomTaskbarWindow : Window
    {
        #region P/Invoke Definitions
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        private const uint ABM_NEW = 0x0000;
        private const uint ABM_REMOVE = 0x0001;
        private const uint ABM_QUERYPOS = 0x0002;
        private const uint ABM_SETPOS = 0x0003;

        private const uint ABE_LEFT = 0;
        private const uint ABE_TOP = 1;
        private const uint ABE_RIGHT = 2;
        private const uint ABE_BOTTOM = 3;

        private const uint GW_OWNER = 4;
        private const uint WM_CLOSE = 0x0010;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const int DWMWA_CLOAKED = 14;
        #endregion

        #region Fields & Properties
        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private string _currentStyle = "Standard";

        private string _currentPosition = "Bottom";

        private DispatcherTimer _clockTimer;

        private readonly LivePreviewWindow _previewWindow;

        private readonly List<(string processName, Rectangle indicator, Image backIcon, Border appCard, string displayTitle)> _appIndicators = new();

        private Border? _activeDraggedCard = null;
        private Point _dragStartPoint;
        private bool _isTrackingDrag = false;

        public static bool ShowSeconds = false;

        public static bool ShowUnpinnedApps = true;
        public static string UnpinnedDisplayMode = "Inline";
        private bool _showingAllRunningView = false;

        private bool _isLoadingApps = false;
        private bool _reloadRequested = false;

        private int _lastUnpinnedCount = -1;

        private bool _isAppBarRegistered = false;

        private List<AppItem> _allAppsCache = new();

        public static string PositionAnimationStyle = "Spring";

        public static string HoverAnimationStyle = "Standard";

        public static bool ShowHoverBackground = true;

        public readonly DisplayArea MonitorArea;
        public readonly bool IsPrimaryMonitor;

        public static bool MonitorAwareApps = false;

        public static int TaskbarSize { get; set; } = 48;
        public static int TaskbarIconSize { get; set; } = 24;

        private static double _previewDelay = 0.5;
        public static double PreviewDelay
        {
            get => _previewDelay;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    _previewDelay = 0.5;
                }
                else if (value > 100)
                {
                    _previewDelay = Math.Clamp(value / 1000.0, 0, 10);
                }
                else
                {
                    _previewDelay = Math.Clamp(value, 0, 10);
                }
            }
        }

        private DispatcherTimer _previewDelayTimer = new DispatcherTimer();
        private Action? _pendingPreviewAction;

        private static readonly HashSet<string> IgnoredSystemProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "SystemSettings", "ApplicationFrameHost", "SearchHost", "StartMenuExperienceHost",
            "ShellExperienceHost", "TextInputHost", "LockApp", "RuntimeBroker", "dwm", "csrss",
            "taskhostw", "EvolveOS_ShellEnhancer", "EvolveOS_Optimizer", "Progman", "WorkerW",
            "cmd", "conhost", "explorer"
        };
        #endregion

        #region Initialization
        public CustomTaskbarWindow() : this(null) { }

        public CustomTaskbarWindow(DisplayArea? displayArea)
        {
            this.InitializeComponent();

            _currentStyle = SettingsEngine.Taskbar_Style ?? "Standard";

            _hWnd = WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            MonitorArea = displayArea ?? DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            IsPrimaryMonitor = MonitorArea.IsPrimary;

            int exclude = 1;
            Win32Helper.DwmSetWindowAttribute(_hWnd, Win32Helper.DWMWA_EXCLUDED_FROM_PEEK, ref exclude, sizeof(int));

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
            }

            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

            Win32Helper.RemoveWindowBorders(_hWnd);
            Win32Helper.PreventFocusStealing(_hWnd);

            TaskbarOverlayManager.ApplyWidgetStyles(_hWnd);

            _previewWindow = new LivePreviewWindow();

            _previewDelayTimer.Tick += (s, e) =>
            {
                _previewDelayTimer.Stop();
                _pendingPreviewAction?.Invoke();
            };

            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
            UpdateClock();

            StartNetworkListener();

            if (BtnStart != null)
            {
                BtnStart.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(FactoryAnimation.StartButton_PointerPressed), true);
                BtnStart.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(FactoryAnimation.StartButton_PointerReleased), true);
                BtnStart.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(FactoryAnimation.StartButton_PointerReleased), true);
            }

            _ = LoadPinnedAppsAsync();

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded += (s, e) =>
                {
                    SetAlignment(TaskbarManager.CurrentAlignment);
                };
            }

            if (!IsPrimaryMonitor)
            {
                if (BtnChevron != null) BtnChevron.Visibility = Visibility.Collapsed;
                if (BtnQuickSettings != null) BtnQuickSettings.Visibility = Visibility.Collapsed;
                if (QuickSettingsIconsPanel != null) QuickSettingsIconsPanel.Visibility = Visibility.Collapsed;

                if (BtnClock != null) BtnClock.IsHitTestVisible = false;
            }

            UpdateSizes();
        }
        #endregion

        #region Sizing Engine
        private void UpdateSizes()
        {
            int btnSize = TaskbarSize - 8;

            if (BtnStart != null)
            {
                BtnStart.Width = btnSize;
                BtnStart.Height = btnSize;
            }
            if (StartIconImage != null)
            {
                StartIconImage.Width = TaskbarIconSize;
                StartIconImage.Height = TaskbarIconSize;
            }

            if (BtnChevron != null)
            {
                BtnChevron.MinWidth = TaskbarSize - 4;
                BtnChevron.Height = btnSize;
            }
            if (ChevronIcon != null)
            {
                ChevronIcon.FontSize = Math.Max(10, TaskbarIconSize - 10);
            }

            if (BtnQuickSettings != null)
            {
                BtnQuickSettings.MinWidth = TaskbarSize - 4;
                BtnQuickSettings.Height = btnSize;
            }

            if (BtnClock != null)
            {
                BtnClock.MinWidth = TaskbarSize - 4;
                BtnClock.Height = btnSize;
            }

            int trayIconSize = Math.Max(12, TaskbarIconSize - 8);
            if (BatteryIcon != null) BatteryIcon.FontSize = trayIconSize;
            if (NetworkIcon != null) NetworkIcon.FontSize = trayIconSize;
            if (VolumeIcon != null) VolumeIcon.FontSize = trayIconSize;
        }
        #endregion

        #region Process & Window Management
        private static bool IsRealTopLevelWindow(IntPtr hWnd)
        {
            if (!IsWindowVisible(hWnd)) return false;
            if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero) return false;

            long exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;

            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            {
                return false;
            }

            if (GetWindowRect(hWnd, out RECT r))
            {
                if (r.Right - r.Left <= 0 || r.Bottom - r.Top <= 0) return false;
            }

            StringBuilder sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            if (string.IsNullOrWhiteSpace(sb.ToString()))
            {
                StringBuilder cb = new StringBuilder(256);
                GetClassName(hWnd, cb, 256);
                if (cb.ToString() != "CabinetWClass") return false;
            }

            return true;
        }

        private bool IsWindowOnThisMonitor(IntPtr hWnd)
        {
            IntPtr windowMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            IntPtr taskbarMonitor = MonitorFromWindow(_hWnd, MONITOR_DEFAULTTONEAREST);
            return windowMonitor == taskbarMonitor;
        }

        private static string NormalizeProcessName(string rawName, string shortcutTitle = "", string targetExePath = "")
        {
            if (!string.IsNullOrWhiteSpace(targetExePath) && File.Exists(targetExePath))
            {
                string exeName = System.IO.Path.GetFileNameWithoutExtension(targetExePath);
                if (!string.IsNullOrEmpty(exeName))
                {
                    return exeName.ToLowerInvariant();
                }
            }

            string name = rawName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
            }

            if (name.Equals("File Explorer", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Windows Explorer", StringComparison.OrdinalIgnoreCase) ||
                shortcutTitle.Contains("Explorer", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                return "explorer";
            }

            return name.ToLowerInvariant();
        }

        private List<IntPtr> GetAppWindowHandles(string processName)
        {
            List<IntPtr> handles = new List<IntPtr>();

            if (string.IsNullOrWhiteSpace(processName)) return handles;

            string norm = NormalizeProcessName(processName);
            if (string.IsNullOrWhiteSpace(norm)) return handles;

            bool isExplorer = norm.Equals("explorer", StringComparison.OrdinalIgnoreCase);

            HashSet<uint> targetPids = new HashSet<uint>();

            try
            {
                if (!isExplorer)
                {
                    var procs = Process.GetProcessesByName(norm);
                    foreach (var p in procs) targetPids.Add((uint)p.Id);
                }
            }
            catch { return handles; }

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsRealTopLevelWindow(hWnd)) return true;

                if (MonitorAwareApps && !IsWindowOnThisMonitor(hWnd)) return true;

                if (isExplorer)
                {
                    StringBuilder sb = new StringBuilder(256);
                    GetClassName(hWnd, sb, sb.Capacity);
                    if (sb.ToString() == "CabinetWClass")
                    {
                        handles.Add(hWnd);
                    }
                }
                else
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    if (targetPids.Contains(pid))
                    {
                        handles.Add(hWnd);
                    }
                }
                return true;
            }, IntPtr.Zero);

            return handles;
        }

        private List<(string processName, string exePath, string windowTitle)> GetUnpinnedRunningApps(HashSet<string> pinnedNormalizedNames)
        {
            var unpinned = new List<(string processName, string exePath, string windowTitle)>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsRealTopLevelWindow(hWnd)) return true;

                if (MonitorAwareApps && !IsWindowOnThisMonitor(hWnd)) return true;

                StringBuilder cb = new StringBuilder(256);
                GetClassName(hWnd, cb, cb.Capacity);
                if (cb.ToString() == "CabinetWClass") return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                try
                {
                    var proc = Process.GetProcessById((int)pid);
                    string procName = proc.ProcessName;
                    string normProc = NormalizeProcessName(procName);

                    if (IgnoredSystemProcesses.Contains(procName) || IgnoredSystemProcesses.Contains(normProc))
                    {
                        return true;
                    }

                    if (!pinnedNormalizedNames.Contains(normProc) &&
                        !unpinned.Any(x => x.processName.Equals(normProc, StringComparison.OrdinalIgnoreCase)))
                    {
                        string exePath = string.Empty;
                        try { exePath = proc.MainModule?.FileName ?? string.Empty; } catch { }

                        StringBuilder sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, 256);
                        string title = sb.ToString();

                        unpinned.Add((normProc, exePath, title));
                    }
                }
                catch { }

                return true;
            }, IntPtr.Zero);

            return unpinned;
        }

        private void UpdateAppIndicators()
        {
            foreach (var item in _appIndicators)
            {
                if (string.IsNullOrEmpty(item.processName))
                {
                    item.indicator.Visibility = Visibility.Collapsed;
                    continue;
                }

                List<IntPtr> handles = GetAppWindowHandles(item.processName);
                bool isRunning = handles.Count > 0;

                item.indicator.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;

                object? currentTip = ToolTipService.GetToolTip(item.appCard);

                if (isRunning)
                {
                    if (currentTip != null)
                    {
                        ToolTipService.SetToolTip(item.appCard, null);
                    }
                }
                else
                {
                    if (currentTip == null)
                    {
                        ToolTipService.SetToolTip(item.appCard, item.displayTitle);
                    }
                }
            }

            if (ShowUnpinnedApps)
            {
                bool isScrollMode = UnpinnedDisplayMode.Equals("Scroll", StringComparison.OrdinalIgnoreCase);
                var cardsToRemove = new List<UIElement>();

                foreach (var child in PinnedAppsPanel.Children)
                {
                    if (child is Border card && card.Tag is string tagStr)
                    {
                        if (tagStr.StartsWith("FOLDER:"))
                        {
                            if (isScrollMode && _showingAllRunningView)
                            {
                                cardsToRemove.Add(card);
                            }
                            continue;
                        }

                        bool isUnpinned = tagStr.StartsWith("UNPINNED:");
                        string processName = isUnpinned ? tagStr.Substring("UNPINNED:".Length) : GetProcessNameFromShortcut(tagStr);

                        var handles = GetAppWindowHandles(processName);
                        if (handles.Count == 0)
                        {
                            if (isUnpinned || (isScrollMode && _showingAllRunningView))
                            {
                                cardsToRemove.Add(card);
                            }
                        }
                    }
                }

                if (cardsToRemove.Count > 0)
                {
                    foreach (var card in cardsToRemove)
                    {
                        PinnedAppsPanel.Children.Remove(card);
                    }
                    _appIndicators.RemoveAll(i => GetAppWindowHandles(i.processName).Count == 0);
                }

                if (!isScrollMode || _showingAllRunningView)
                {
                    var shortcuts = GetPinnedTaskbarApps();
                    var pinnedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string lnk in shortcuts)
                    {
                        pinnedNames.Add(GetProcessNameFromShortcut(lnk));
                    }

                    int currentUnpinnedCount = GetUnpinnedRunningApps(pinnedNames).Count;
                    if (currentUnpinnedCount != _lastUnpinnedCount)
                    {
                        _lastUnpinnedCount = currentUnpinnedCount;
                        _ = LoadPinnedAppsAsync();
                    }
                }
            }
        }
        #endregion

        #region Shortcut Parsing
        public static List<string> GetPinnedTaskbarApps()
        {
            List<string> pinnedApps = new List<string>();
            try
            {
                string taskbarPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"
                );

                if (Directory.Exists(taskbarPath))
                {
                    string[] shortcuts = Directory.GetFiles(taskbarPath, "*.lnk");
                    foreach (string shortcut in shortcuts)
                    {
                        pinnedApps.Add(shortcut);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load shortcuts: {ex.Message}");
            }

            return pinnedApps;
        }

        public static string ParseShortcut(string lnkPath)
        {
            try
            {
                IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(lnkPath);

                string target = shortcut.TargetPath ?? string.Empty;
                string args = shortcut.Arguments ?? string.Empty;

                if (target.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase) && args.IndexOf(@"shell:appsfolder\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    int idx = args.IndexOf(@"shell:appsfolder\", StringComparison.OrdinalIgnoreCase);
                    return args.Substring(idx + 17).Trim();
                }

                target = target.Trim().Trim('"', '\'');
                return target;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Shortcut Parse Error: " + ex.Message);
                return string.Empty;
            }
        }

        public static void PinItemToTaskbar(string targetPath)
        {
            Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(targetPath)) return;

                    if (!File.Exists(targetPath) && !Directory.Exists(targetPath)) return;

                    string taskbarPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"
                    );

                    if (!Directory.Exists(taskbarPath))
                    {
                        Directory.CreateDirectory(taskbarPath);
                    }

                    string itemName = System.IO.Path.GetFileNameWithoutExtension(targetPath);
                    if (string.IsNullOrEmpty(itemName))
                    {
                        itemName = System.IO.Path.GetFileName(targetPath);
                    }

                    string lnkPath = System.IO.Path.Combine(taskbarPath, $"{itemName}.lnk");

                    int counter = 1;
                    while (File.Exists(lnkPath))
                    {
                        lnkPath = System.IO.Path.Combine(taskbarPath, $"{itemName} ({counter}).lnk");
                        counter++;
                    }

                    Type? wshShellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (wshShellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(wshShellType)!;
                        dynamic shortcut = shell.CreateShortcut(lnkPath);
                        shortcut.TargetPath = targetPath;
                        shortcut.Save();

                        Marshal.ReleaseComObject(shortcut);
                        Marshal.ReleaseComObject(shell);
                    }

                    TaskbarManager.ReloadAll();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to pin item to taskbar: {ex.Message}");
                }
            });
        }

        private string GetProcessNameFromShortcut(string lnkPath)
        {
            string shortcutName = System.IO.Path.GetFileNameWithoutExtension(lnkPath);
            string targetExe = ParseShortcut(lnkPath);
            if (!string.IsNullOrWhiteSpace(targetExe))
            {
                targetExe = Environment.ExpandEnvironmentVariables(targetExe);
            }
            return NormalizeProcessName(shortcutName, shortcutName, targetExe);
        }
        #endregion

        #region App Loading & Icon Extraction
        public void ReloadTaskbar()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                _ = LoadPinnedAppsAsync();

                if (this.Content is FrameworkElement root)
                {
                    var currentTheme = root.RequestedTheme;
                    root.RequestedTheme = root.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
                    root.RequestedTheme = currentTheme;
                }

                if (ClockText != null && DateText != null)
                {
                    if (Application.Current.Resources.TryGetValue("AppFontSizeBase", out var baseObj) && baseObj is double baseSize)
                    {
                        ClockText.FontSize = baseSize;
                    }

                    if (Application.Current.Resources.TryGetValue("AppFontSizeSmall", out var smallObj) && smallObj is double smallSize)
                    {
                        DateText.FontSize = smallSize;
                    }
                }

                UpdateSizes();

                if (_appWindow.IsVisible)
                {
                    ShowDock();
                }
            });
        }

        private async Task LoadPinnedAppsAsync()
        {
            if (_isLoadingApps)
            {
                _reloadRequested = true;
                return;
            }

            _isLoadingApps = true;
            try
            {
                do
                {
                    _reloadRequested = false;
                    await LoadPinnedAppsInternalAsync();
                } while (_reloadRequested);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPinnedAppsAsync Faulted: {ex.Message}");
            }
            finally
            {
                _isLoadingApps = false;
            }
        }

        private async Task LoadPinnedAppsInternalAsync()
        {
            if (_allAppsCache.Count == 0)
            {
                _allAppsCache = await StartMenuHelper.GetAllAppsAsync();
            }

            PinnedAppsPanel.Children.Clear();
            _appIndicators.Clear();

            List<string> shortcuts = GetPinnedTaskbarApps();
            var pinnedNormalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string lnk in shortcuts)
            {
                pinnedNormalizedNames.Add(GetProcessNameFromShortcut(lnk));
            }

            string savedOrderStr = SettingsEngine.TaskbarPinnedAppsOrder;
            if (!string.IsNullOrWhiteSpace(savedOrderStr))
            {
                var savedOrder = savedOrderStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var sortedShortcuts = new List<string>();

                foreach (var name in savedOrder)
                {
                    var match = shortcuts.Find(s => string.Equals(System.IO.Path.GetFileName(s), name, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        sortedShortcuts.Add(match);
                        shortcuts.Remove(match);
                    }
                }
                sortedShortcuts.AddRange(shortcuts);
                shortcuts = sortedShortcuts;
            }

            bool isScrollMode = UnpinnedDisplayMode.Equals("Scroll", StringComparison.OrdinalIgnoreCase);
            bool showOnlyRunning = isScrollMode && _showingAllRunningView;

            if (!showOnlyRunning)
            {
                foreach (string lnk in shortcuts)
                {
                    if (!File.Exists(lnk)) continue;
                    await CreateAppCardAsync(lnk, lnk, isShortcut: true);
                }

                string savedFilteredFolders = SettingsEngine.Taskbar_FilteredFolders;
                if (!string.IsNullOrWhiteSpace(savedFilteredFolders))
                {
                    var folderPaths = savedFilteredFolders.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string folderPath in folderPaths)
                    {
                        if (Directory.Exists(folderPath))
                        {
                            string folderName = System.IO.Path.GetFileName(folderPath);
                            if (string.IsNullOrEmpty(folderName)) folderName = folderPath;

                            await CreateAppCardAsync(
                                identifier: folderName,
                                pathOrLnk: folderPath,
                                isShortcut: false,
                                windowTitle: folderName
                            );
                        }
                    }
                }
            }
            else
            {
                foreach (string lnk in shortcuts)
                {
                    if (!File.Exists(lnk)) continue;

                    string targetExe = ParseShortcut(lnk);
                    if (!string.IsNullOrWhiteSpace(targetExe)) targetExe = Environment.ExpandEnvironmentVariables(targetExe);
                    if (Directory.Exists(targetExe)) continue;

                    string pName = GetProcessNameFromShortcut(lnk);
                    if (GetAppWindowHandles(pName).Count > 0)
                    {
                        await CreateAppCardAsync(lnk, lnk, isShortcut: true);
                    }
                }
            }

            if (ShowUnpinnedApps)
            {
                var unpinnedApps = GetUnpinnedRunningApps(pinnedNormalizedNames);
                foreach (var unpinned in unpinnedApps)
                {
                    var handles = GetAppWindowHandles(unpinned.processName);
                    if (handles.Count > 0)
                    {
                        await CreateAppCardAsync(unpinned.processName, unpinned.exePath, isShortcut: false, windowTitle: unpinned.windowTitle);
                    }
                }
            }

            UpdateAppIndicators();
        }

        private async Task CreateAppCardAsync(string identifier, string pathOrLnk, bool isShortcut, string windowTitle = "")
        {
            string targetExe = isShortcut ? ParseShortcut(pathOrLnk) : pathOrLnk;
            if (!string.IsNullOrWhiteSpace(targetExe)) targetExe = Environment.ExpandEnvironmentVariables(targetExe);

            bool isDirectory = Directory.Exists(targetExe);

            string processName = isShortcut ? GetProcessNameFromShortcut(pathOrLnk) : NormalizeProcessName(identifier, targetExePath: targetExe);
            string shortcutName = isShortcut ? System.IO.Path.GetFileNameWithoutExtension(pathOrLnk) : identifier;

            AppItem? matchingApp = null;
            if (_allAppsCache != null && !isDirectory)
            {
                matchingApp = _allAppsCache.FirstOrDefault(a =>
                    (a.ExecutablePath != null && a.ExecutablePath.Equals(targetExe, StringComparison.OrdinalIgnoreCase)) ||
                    (a.Name != null && a.Name.Equals(shortcutName, StringComparison.OrdinalIgnoreCase)));
            }

            var appItem = matchingApp ?? new AppItem
            {
                Name = shortcutName,
                ExecutablePath = targetExe,
                IsUwp = targetExe.Contains("!"),
                IconScale = 1.0,
                FallbackGlyph = isDirectory ? "\xE8B7" : "\xE738"
            };

            Grid iconGrid = new Grid();

            Grid iconContainer = new Grid
            {
                Width = TaskbarIconSize + 8,
                Height = TaskbarIconSize + 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            FontIcon fallbackIcon = new FontIcon
            {
                Glyph = appItem.FallbackGlyph ?? "\xE738",
                FontSize = TaskbarIconSize - 4,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Foreground = isDirectory ? new SolidColorBrush(Microsoft.UI.Colors.Gold) : new SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = appItem.IconSource == null ? Visibility.Visible : Visibility.Collapsed
            };

            Image backIcon = new Image
            {
                Width = TaskbarIconSize,
                Height = TaskbarIconSize,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, -6, 0, 0),
                Opacity = 0.5,
                Visibility = Visibility.Collapsed,
                Source = appItem.IconSource
            };

            Image appIcon = new Image
            {
                Width = TaskbarIconSize,
                Height = TaskbarIconSize,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0),
                Source = appItem.IconSource,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform { ScaleX = appItem.IconScale, ScaleY = appItem.IconScale },
                Visibility = appItem.IconSource != null ? Visibility.Visible : Visibility.Collapsed
            };

            iconContainer.Children.Add(fallbackIcon);
            iconContainer.Children.Add(backIcon);
            iconContainer.Children.Add(appIcon);

            bool isVertical = _currentPosition == "Left" || _currentPosition == "Right";

            Rectangle indicator = new Rectangle
            {
                Width = isVertical ? 3 : 17,
                Height = isVertical ? 17 : 3,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = new SolidColorBrush(Colors.LightGray),
                HorizontalAlignment = isVertical ? (_currentPosition == "Left" ? HorizontalAlignment.Left : HorizontalAlignment.Right) : HorizontalAlignment.Center,
                VerticalAlignment = isVertical ? VerticalAlignment.Center : (_currentPosition == "Top" ? VerticalAlignment.Top : VerticalAlignment.Bottom),
                Margin = new Thickness(0, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };

            iconGrid.Children.Add(iconContainer);
            iconGrid.Children.Add(indicator);

            Border appCard = new Border
            {
                Width = TaskbarSize - 8,
                Height = TaskbarSize - 8,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(Colors.Transparent),
                CornerRadius = new CornerRadius(4),
                Child = iconGrid,
                Tag = isDirectory ? $"FOLDER:{targetExe}" : (isShortcut ? pathOrLnk : $"UNPINNED:{processName}")
            };

            string displayTitle = isShortcut ? shortcutName : (string.IsNullOrEmpty(windowTitle) ? processName : windowTitle);
            ToolTipService.SetToolTip(appCard, displayTitle);

            Action launchNewInstance = () =>
            {
                try
                {
                    if (isDirectory)
                    {
                        if (!isShortcut)
                        {
                            ShowFolderSubmenu(appCard, targetExe);
                        }
                        else
                        {
                            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{targetExe}\"") { UseShellExecute = true });
                        }
                        return;
                    }

                    if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
                        return;
                    }

                    string launchPath = isShortcut ? pathOrLnk : targetExe;

                    if (string.IsNullOrEmpty(launchPath) || !File.Exists(launchPath))
                    {
                        if (processName.Equals("cmd", StringComparison.OrdinalIgnoreCase)) launchPath = "cmd.exe";
                        else return;
                    }

                    Process.Start(new ProcessStartInfo(launchPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to launch new instance of {displayTitle}: {ex.Message}");
                }
            };

            MenuFlyout contextFlyout = new MenuFlyout();

            contextFlyout.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

            Style flyoutStyle = new Style(typeof(MenuFlyoutPresenter));

            flyoutStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
            flyoutStyle.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(8)));
            flyoutStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))));
            flyoutStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));

            contextFlyout.MenuFlyoutPresenterStyle = flyoutStyle;

            var launchItem = new MenuFlyoutItem
            {
                Text = displayTitle,
                FontWeight = FontWeights.SemiBold
            };
            if (appItem.IconSource != null) launchItem.Icon = new ImageIcon { Source = appItem.IconSource };
            launchItem.Click += (s, e) => launchNewInstance();

            contextFlyout.Items.Add(launchItem);
            contextFlyout.Items.Add(new MenuFlyoutSeparator());

            if (!isDirectory)
            {
                var closeItem = new MenuFlyoutItem { Text = "Close window", Icon = new FontIcon { Glyph = "\uE8BB" } };
                closeItem.Click += (s, e) =>
                {
                    var handles = GetAppWindowHandles(processName);
                    foreach (var h in handles) { PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); }
                };
                contextFlyout.Items.Add(closeItem);
            }

            if (isShortcut || isDirectory)
            {
                var unpinItem = new MenuFlyoutItem { Text = "Unpin from taskbar", Icon = new FontIcon { Glyph = "\uE196" } };
                unpinItem.Click += (s, e) =>
                {
                    try
                    {
                        if (isDirectory && !isShortcut)
                        {
                            string saved = SettingsEngine.Taskbar_FilteredFolders;
                            var paths = saved.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                            paths.RemoveAll(p => p.Equals(targetExe, StringComparison.OrdinalIgnoreCase));
                            SettingsEngine.Taskbar_FilteredFolders = string.Join(";", paths);
                        }
                        else if (File.Exists(pathOrLnk))
                        {
                            File.Delete(pathOrLnk);
                        }
                        _ = LoadPinnedAppsAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to unpin: {ex.Message}");
                    }
                };
                contextFlyout.Items.Add(new MenuFlyoutSeparator());
                contextFlyout.Items.Add(unpinItem);
            }

            appCard.ContextFlyout = contextFlyout;
            if (!isDirectory) _appIndicators.Add((processName, indicator, backIcon, appCard, displayTitle));

            appCard.PointerPressed += (s, e) =>
            {
                _previewDelayTimer.Stop();
                _pendingPreviewAction = null;

                FactoryAnimation.AnimateAppCardClickDown(appCard);
                var props = e.GetCurrentPoint(appCard).Properties;

                if (props.IsMiddleButtonPressed)
                {
                    launchNewInstance();
                    e.Handled = true;
                }
            };
            appCard.PointerReleased += (s, e) => FactoryAnimation.AnimateAppCardClickUp(appCard);
            appCard.PointerCanceled += (s, e) => FactoryAnimation.AnimateAppCardClickUp(appCard);

            appCard.Tapped += (s, e) =>
            {
                _previewDelayTimer.Stop();
                _pendingPreviewAction = null;

                if (_isTrackingDrag) return;
                _previewWindow.HidePreview();

                var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
                bool isShiftPressed = (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

                if (isShiftPressed || isDirectory)
                {
                    launchNewInstance();
                    return;
                }

                bool activatedExisting = false;
                if (!string.IsNullOrEmpty(processName))
                {
                    var handles = GetAppWindowHandles(processName);
                    if (handles.Count > 0)
                    {
                        IntPtr handle = handles[0];
                        if (Win32Helper.IsIconic(handle))
                        {
                            Win32Helper.ShowWindow(handle, Win32Helper.SW_RESTORE);
                        }
                        Win32Helper.SetForegroundWindow(handle);
                        activatedExisting = true;
                    }
                }

                if (!activatedExisting && isShortcut)
                {
                    launchNewInstance();
                }
            };

            appCard.PointerEntered += (s, e) =>
            {
                if (ShowHoverBackground)
                {
                    appCard.Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
                }

                FactoryAnimation.AnimateAppCardHoverEnter(appCard, HoverAnimationStyle);

                if (_isTrackingDrag || string.IsNullOrEmpty(processName) || isDirectory) return;

                var handles = GetAppWindowHandles(processName);

                if (handles.Count > 0)
                {
                    if (ToolTipService.GetToolTip(appCard) != null) ToolTipService.SetToolTip(appCard, null);
                }
                else
                {
                    if (ToolTipService.GetToolTip(appCard) == null) ToolTipService.SetToolTip(appCard, displayTitle);
                }

                if (handles.Count > 1)
                {
                    backIcon.Visibility = Visibility.Visible;
                    appIcon.Margin = new Thickness(-4, 4, 0, 0);
                }

                if (handles.Count > 0)
                {
                    var transform = appCard.TransformToVisual(null);
                    var localPoint = transform.TransformPoint(new Point(0, 0));

                    int cardScreenX = _appWindow.Position.X + (int)localPoint.X;
                    int cardScreenY = _appWindow.Position.Y + (int)localPoint.Y;

                    _pendingPreviewAction = () =>
                    {
                        _previewWindow.ShowPreviews(handles, cardScreenX, cardScreenY, (int)appCard.Width, (int)appCard.Height);
                    };

                    if (PreviewDelay > 0)
                    {
                        _previewDelayTimer.Stop();
                        _previewDelayTimer.Interval = TimeSpan.FromSeconds(PreviewDelay);
                        _previewDelayTimer.Start();
                    }
                    else
                    {
                        _pendingPreviewAction.Invoke();
                    }
                }
            };

            appCard.PointerExited += (s, e) =>
            {
                _previewDelayTimer.Stop();
                _pendingPreviewAction = null;

                appCard.Background = new SolidColorBrush(Colors.Transparent);
                FactoryAnimation.AnimateAppCardHoverExit(appCard, HoverAnimationStyle);

                backIcon.Visibility = Visibility.Collapsed;
                appIcon.Margin = new Thickness(0, 0, 0, 0);

                _previewWindow.StartHideTimer();
            };

            if (isShortcut && !isDirectory)
            {
                appCard.PointerPressed += (s, e) =>
                {
                    var props = e.GetCurrentPoint(PinnedAppsPanel).Properties;
                    if (!props.IsLeftButtonPressed) return;

                    appCard.Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));

                    _activeDraggedCard = appCard;
                    _isTrackingDrag = false;
                    _dragStartPoint = e.GetCurrentPoint(PinnedAppsPanel).Position;
                    appCard.CapturePointer(e.Pointer);
                };

                appCard.PointerMoved += (s, e) =>
                {
                    if (_activeDraggedCard != appCard) return;

                    var currentPoint = e.GetCurrentPoint(PinnedAppsPanel).Position;
                    double deltaX = currentPoint.X - _dragStartPoint.X;

                    if (!_isTrackingDrag && Math.Abs(deltaX) > 4)
                    {
                        _isTrackingDrag = true;
                        _previewWindow.HidePreview();
                        Canvas.SetZIndex(appCard, 100);
                        appCard.Opacity = 0.8;
                    }

                    if (_isTrackingDrag)
                    {
                        appCard.Translation = new Vector3((float)deltaX, 0, 10f);
                    }
                };

                Action releaseDrag = () =>
                {
                    if (_activeDraggedCard == appCard)
                    {
                        if (_isTrackingDrag)
                        {
                            var currentPoint = _dragStartPoint.X + appCard.Translation.X + (appCard.ActualWidth / 2);

                            int targetIndex = PinnedAppsPanel.Children.Count - 1;
                            for (int i = 0; i < PinnedAppsPanel.Children.Count; i++)
                            {
                                if (PinnedAppsPanel.Children[i] is FrameworkElement child && child != appCard)
                                {
                                    var childPos = child.TransformToVisual(PinnedAppsPanel).TransformPoint(new Point(0, 0));
                                    if (currentPoint < childPos.X + (child.ActualWidth / 2))
                                    {
                                        targetIndex = i;
                                        if (PinnedAppsPanel.Children.IndexOf(appCard) < targetIndex) targetIndex--;
                                        break;
                                    }
                                }
                            }

                            PinnedAppsPanel.Children.Remove(appCard);
                            PinnedAppsPanel.Children.Insert(targetIndex, appCard);

                            var newOrder = new List<string>();
                            foreach (var element in PinnedAppsPanel.Children)
                            {
                                if (element is Border b && b.Tag is string savedLnk && !savedLnk.StartsWith("FOLDER:"))
                                {
                                    newOrder.Add(System.IO.Path.GetFileName(savedLnk));
                                }
                            }
                            SettingsEngine.TaskbarPinnedAppsOrder = string.Join(",", newOrder);
                        }

                        appCard.Translation = Vector3.Zero;
                        appCard.Opacity = 1.0;
                        appCard.Background = new SolidColorBrush(Colors.Transparent);
                        Canvas.SetZIndex(appCard, 0);
                        try { appCard.ReleasePointerCaptures(); } catch { }

                        _activeDraggedCard = null;
                        DispatcherQueue.TryEnqueue(() => _isTrackingDrag = false);
                    }
                };

                appCard.PointerReleased += (s, e) => releaseDrag();
                appCard.PointerCanceled += (s, e) => releaseDrag();
            }

            PinnedAppsPanel.Children.Add(appCard);

            appItem.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppItem.IconSource) && appItem.IconSource != null)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        appIcon.Source = appItem.IconSource;
                        backIcon.Source = appItem.IconSource;
                        launchItem.Icon = new ImageIcon { Source = appItem.IconSource };

                        appIcon.Visibility = Visibility.Visible;
                        fallbackIcon.Visibility = Visibility.Collapsed;
                    });
                }
            };

            if (appItem.IconSource == null && !isDirectory)
            {
                _ = LoadIconSafelyAsync(appItem);
            }
        }

        private void ShowFolderSubmenu(UIElement target, string path)
        {
            MenuFlyout flyout = new MenuFlyout();

            Style flyoutStyle = new Style(typeof(MenuFlyoutPresenter));
            flyoutStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(220, 20, 20, 20))));
            flyoutStyle.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(8)));
            flyoutStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))));
            flyoutStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            flyout.MenuFlyoutPresenterStyle = flyoutStyle;

            bool useSubmenus = SettingsEngine.Taskbar_ShowFoldersAsSubmenus;
            BuildMenuHierarchy(flyout.Items, path, useSubmenus, 0);

            flyout.ShowAt(target, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.TopEdgeAlignedLeft
            });
        }

        private void BuildMenuHierarchy(IList<MenuFlyoutItemBase> parentItems, string currentPath, bool useSubmenus, int depth)
        {
            try
            {
                var dirs = Directory.GetDirectories(currentPath);
                var files = Directory.GetFiles(currentPath);

                if (dirs.Length == 0 && files.Length == 0)
                {
                    parentItems.Add(new MenuFlyoutItem { Text = "Empty Folder", IsEnabled = false });
                    return;
                }

                foreach (var d in dirs)
                {
                    string folderName = System.IO.Path.GetFileName(d);
                    if (string.IsNullOrEmpty(folderName)) folderName = d;

                    if (useSubmenus && depth < 5)
                    {
                        var subItem = new MenuFlyoutSubItem
                        {
                            Text = folderName,
                            Icon = new FontIcon { Glyph = "\xE8B7", FontFamily = new FontFamily("Segoe Fluent Icons"), Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gold) }
                        };

                        BuildMenuHierarchy(subItem.Items, d, useSubmenus, depth + 1);
                        parentItems.Add(subItem);
                    }
                    else
                    {
                        var item = new MenuFlyoutItem
                        {
                            Text = folderName,
                            Icon = new FontIcon { Glyph = "\xE8B7", FontFamily = new FontFamily("Segoe Fluent Icons"), Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gold) }
                        };
                        item.Click += (s, e) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{d}\"") { UseShellExecute = true });
                        parentItems.Add(item);
                    }
                }

                if (dirs.Length > 0 && files.Length > 0)
                {
                    parentItems.Add(new MenuFlyoutSeparator());
                }

                foreach (var f in files)
                {
                    string fileName = System.IO.Path.GetFileName(f);
                    var item = new MenuFlyoutItem
                    {
                        Text = fileName,
                        Icon = new FontIcon { Glyph = "\xE7C3", FontFamily = new FontFamily("Segoe Fluent Icons") }
                    };
                    item.Click += (s, e) => Process.Start(new ProcessStartInfo(f) { UseShellExecute = true });
                    parentItems.Add(item);
                }
            }
            catch
            {
                parentItems.Add(new MenuFlyoutItem { Text = "Access Denied", IsEnabled = false });
            }
        }

        private async Task LoadIconSafelyAsync(AppItem item)
        {
            try
            {
                var src = await StartMenuHelper.ExtractAppIconAsync(item);
                if (src != null)
                {
                    item.IconSource = src;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Taskbar dynamic icon extraction failed: {ex.Message}");
            }
        }
        #endregion

        #region Dock Visibility

        public void ShowDock()
        {
            try
            {
                Win32Helper.HideNativeTaskbar();

                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
                    {
                        if (key != null)
                        {
                            object? val = key.GetValue("MMTaskbarEnabled");
                            if (val == null || (int)val != 0)
                            {
                                key.SetValue("MMTaskbarEnabled", 0, RegistryValueKind.DWord);
                                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "TraySettings", SMTO_ABORTIFHUNG, 1000, out _);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Registry override failed: {ex.Message}");
                }

                IntPtr nativeTray = FindWindow("Shell_TrayWnd", null);
                if (nativeTray != IntPtr.Zero)
                {
                    APPBARDATA abdNative = new APPBARDATA();
                    abdNative.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
                    abdNative.hWnd = nativeTray;
                    abdNative.lParam = 3;
                    SHAppBarMessage(0x000A, ref abdNative);
                }

                int screenX = MonitorArea!.OuterBounds.X;
                int screenY = MonitorArea.OuterBounds.Y;
                int screenWidth = MonitorArea.OuterBounds.Width;
                int screenHeight = MonitorArea.OuterBounds.Height;

                int taskbarSize = TaskbarSize;
                int margin = (_currentStyle == "Floating") ? 5 : 0;
                int reservedSpace = taskbarSize + (margin * 2);

                int x = 0, y = 0, w = 0, h = 0;
                uint edge;

                switch (_currentPosition)
                {
                    case "Top": edge = ABE_TOP; break;
                    case "Left": edge = ABE_LEFT; break;
                    case "Right": edge = ABE_RIGHT; break;
                    case "Bottom": default: edge = ABE_BOTTOM; break;
                }

                switch (edge)
                {
                    case ABE_TOP:
                        w = screenWidth - (margin * 2); h = taskbarSize;
                        x = screenX + margin; y = screenY + margin;
                        break;
                    case ABE_LEFT:
                        w = taskbarSize; h = screenHeight - (margin * 2);
                        x = screenX + margin; y = screenY + margin;
                        break;
                    case ABE_RIGHT:
                        w = taskbarSize; h = screenHeight - (margin * 2);
                        x = screenX + screenWidth - taskbarSize - margin; y = screenY + margin;
                        break;
                    case ABE_BOTTOM:
                    default:
                        w = screenWidth - (margin * 2); h = taskbarSize;
                        x = screenX + margin; y = screenY + screenHeight - taskbarSize - margin;
                        break;
                }

                if (w < 10) w = 10;
                if (h < 10) h = 10;

                if (_isAppBarRegistered)
                {
                    APPBARDATA abdRemove = new APPBARDATA();
                    abdRemove.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
                    abdRemove.hWnd = _hWnd;
                    SHAppBarMessage(ABM_REMOVE, ref abdRemove);
                    _isAppBarRegistered = false;
                }

                _appWindow.MoveAndResize(new RectInt32(x, y, w, h));
                _appWindow.Show();
                SetWindowPos(_hWnd, IntPtr.Zero, x, y, w, h, 0x0040);

                APPBARDATA abd = new APPBARDATA();
                abd.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
                abd.hWnd = _hWnd;
                abd.uCallbackMessage = (uint)(0x0400 + (_appWindow.Id.Value & 0xFFFF));
                abd.uEdge = edge;

                SHAppBarMessage(ABM_NEW, ref abd);
                _isAppBarRegistered = true;

                abd.rc.Left = screenX + 1;
                abd.rc.Top = screenY + 1;
                abd.rc.Right = screenX + screenWidth - 1;
                abd.rc.Bottom = screenY + screenHeight - 1;

                switch (edge)
                {
                    case ABE_TOP: abd.rc.Bottom = abd.rc.Top + reservedSpace; break;
                    case ABE_LEFT: abd.rc.Right = abd.rc.Left + reservedSpace; break;
                    case ABE_RIGHT: abd.rc.Left = abd.rc.Right - reservedSpace; break;
                    case ABE_BOTTOM: default: abd.rc.Top = abd.rc.Bottom - reservedSpace; break;
                }

                SHAppBarMessage(ABM_QUERYPOS, ref abd);

                switch (edge)
                {
                    case ABE_TOP:
                        abd.rc.Top = screenY;
                        abd.rc.Bottom = screenY + reservedSpace;
                        abd.rc.Left = screenX;
                        abd.rc.Right = screenX + screenWidth;
                        break;
                    case ABE_BOTTOM:
                        abd.rc.Top = screenY + screenHeight - reservedSpace;
                        abd.rc.Bottom = screenY + screenHeight;
                        abd.rc.Left = screenX;
                        abd.rc.Right = screenX + screenWidth;
                        break;
                    case ABE_LEFT:
                        abd.rc.Left = screenX;
                        abd.rc.Right = screenX + reservedSpace;
                        abd.rc.Top = screenY;
                        abd.rc.Bottom = screenY + screenHeight;
                        break;
                    case ABE_RIGHT:
                        abd.rc.Left = screenX + screenWidth - reservedSpace;
                        abd.rc.Right = screenX + screenWidth;
                        abd.rc.Top = screenY;
                        abd.rc.Bottom = screenY + screenHeight;
                        break;
                }

                SHAppBarMessage(ABM_SETPOS, ref abd);

                if (_currentStyle == "Floating")
                {
                    Win32Helper.SetCornerPreference(_hWnd, Win32Helper.DWMWCP_ROUNDSMALL);
                    TaskbarBorder.CornerRadius = new CornerRadius(4);
                }
                else
                {
                    Win32Helper.SetCornerPreference(_hWnd, Win32Helper.DWMWCP_DONOTROUND);
                    TaskbarBorder.CornerRadius = new CornerRadius(0);
                }

                _appWindow.MoveAndResize(new RectInt32(x, y, w, h));
                SetWindowPos(_hWnd, new IntPtr(-1), x, y, w, h, 0x0040 | 0x0010);
                TaskbarOverlayManager.EnsureTopmost(_hWnd);

                string savedAlignment = TaskbarManager.CurrentAlignment;
                SetAlignment(savedAlignment);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowDock error: {ex.Message}");
            }
        }

        public void HideDock()
        {
            _appWindow.Hide();
            Win32Helper.ShowNativeTaskbar();

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
                {
                    if (key != null)
                    {
                        key.SetValue("MMTaskbarEnabled", 1, RegistryValueKind.DWord);

                        IntPtr explorerTray = FindWindow("Shell_TrayWnd", null);
                        if (explorerTray != IntPtr.Zero)
                        {
                            SendMessageTimeout(explorerTray, WM_SETTINGCHANGE, IntPtr.Zero, "TraySettings", SMTO_ABORTIFHUNG, 1000, out _);
                        }
                    }
                }
            }
            catch { }

            IntPtr nativeTray = FindWindow("Shell_TrayWnd", null);
            if (nativeTray != IntPtr.Zero)
            {
                APPBARDATA abdNative = new APPBARDATA();
                abdNative.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
                abdNative.hWnd = nativeTray;
                abdNative.lParam = 2;
                SHAppBarMessage(0x000A, ref abdNative);
            }

            if (_isAppBarRegistered)
            {
                APPBARDATA abd = new APPBARDATA();
                abd.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
                abd.hWnd = _hWnd;
                SHAppBarMessage(ABM_REMOVE, ref abd);
                _isAppBarRegistered = false;
            }
        }

        #endregion

        #region UI Layout & Styling Handlers

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if ((DateTime.Now - App.LastStartMenuCloseTime).TotalMilliseconds < 250) return;

            if (!IsPrimaryMonitor)
            {
                if (Application.Current is App currentApp)
                {
                    currentApp.ToggleStartMenu(MonitorArea, forceCustom: true);
                }
                return;
            }

            if (!SettingsEngine.Shell_StartMenuEnabled)
            {
                SendMessage(_hWnd, 0x0112, new IntPtr(0xF130), IntPtr.Zero);
            }
            else
            {
                if (Application.Current is App currentApp)
                {
                    currentApp.ToggleStartMenu(MonitorArea, forceCustom: false);
                }
            }
        }

        public void ResetUnpinnedScrollView()
        {
            _showingAllRunningView = false;
        }

        private async void TaskbarBorder_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (ShowUnpinnedApps && UnpinnedDisplayMode.Equals("Scroll", StringComparison.OrdinalIgnoreCase))
                {
                    var delta = e.GetCurrentPoint(TaskbarBorder).Properties.MouseWheelDelta;
                    if (delta != 0)
                    {
                        _showingAllRunningView = !_showingAllRunningView;

                        bool isScrollingUp = delta > 0;

                        await FactoryAnimation.PlayScrollTransitionAsync(
                            this.Content as UIElement,
                            CenterPanel,
                            LeftPanel,
                            PinnedAppsPanel,
                            async () => { await LoadPinnedAppsAsync(); },
                            isScrollingUp
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Wheel Transition Error: {ex.Message}");
            }
        }

        public void SetStyle(string style)
        {
            _currentStyle = style;
            if (_appWindow.IsVisible)
            {
                ShowDock();
            }
        }

        public void ApplySizeChanges()
        {
            UpdateSizes();
            if (_appWindow.IsVisible)
            {
                ShowDock();
            }
        }

        public void SetPosition(string position)
        {
            _currentPosition = position;
            try
            {
                bool isVertical = (position == "Left" || position == "Right");
                Orientation orientation = isVertical ? Orientation.Vertical : Orientation.Horizontal;

                if (LeftPanel != null) LeftPanel.Orientation = orientation;
                if (CenterPanel != null) CenterPanel.Orientation = orientation;
                if (PinnedAppsPanel != null) PinnedAppsPanel.Orientation = orientation;
                if (RightPanel != null) RightPanel.Orientation = orientation;

                if (QuickSettingsIconsPanel != null)
                {
                    QuickSettingsIconsPanel.Orientation = orientation;
                }

                if (ClockText != null && DateText != null)
                {
                    ClockText.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Right;
                    DateText.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Right;
                    ClockText.TextAlignment = isVertical ? TextAlignment.Center : TextAlignment.Right;
                    DateText.TextAlignment = isVertical ? TextAlignment.Center : TextAlignment.Right;
                }

                if (ClockContentPanel != null)
                {
                    ClockContentPanel.Orientation = Orientation.Vertical;
                    ClockContentPanel.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Right;
                }

                if (BtnClock != null)
                {
                    if (isVertical)
                    {
                        BtnClock.MaxWidth = TaskbarSize - 4;
                        BtnClock.Padding = new Thickness(2, 0, 2, 0);
                    }
                    else
                    {
                        BtnClock.MaxWidth = double.PositiveInfinity;
                        BtnClock.Padding = new Thickness(12, 0, 12, 0);
                    }
                }

                if (BtnClock?.Content is StackPanel clockSp)
                {
                    clockSp.Orientation = Orientation.Vertical;
                    clockSp.HorizontalAlignment = HorizontalAlignment.Center;
                }

                foreach (var child in PinnedAppsPanel!.Children)
                {
                    if (child is Border card && card.Child is Grid iconGrid)
                    {
                        if (iconGrid.Children.Count > 1 && iconGrid.Children[1] is Rectangle indicator)
                        {
                            if (isVertical)
                            {
                                indicator.Width = 3;
                                indicator.Height = 17;
                                indicator.HorizontalAlignment = (position == "Left") ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                                indicator.VerticalAlignment = VerticalAlignment.Center;
                            }
                            else
                            {
                                indicator.Width = 17;
                                indicator.Height = 3;
                                indicator.HorizontalAlignment = HorizontalAlignment.Center;
                                indicator.VerticalAlignment = (position == "Top") ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                            }
                        }
                    }
                }

                if (isVertical)
                {
                    if (LeftPanel != null)
                    {
                        LeftPanel.HorizontalAlignment = HorizontalAlignment.Center;
                        LeftPanel.VerticalAlignment = VerticalAlignment.Top;
                        LeftPanel.Margin = new Thickness(0, 12, 0, 0);
                    }
                    if (CenterPanel != null)
                    {
                        CenterPanel.HorizontalAlignment = HorizontalAlignment.Center;
                        CenterPanel.VerticalAlignment = VerticalAlignment.Center;
                        CenterPanel.Margin = new Thickness(0);
                    }
                    if (RightPanel != null)
                    {
                        RightPanel.HorizontalAlignment = HorizontalAlignment.Center;
                        RightPanel.VerticalAlignment = VerticalAlignment.Bottom;
                        RightPanel.Margin = new Thickness(0, 0, 0, 12);
                    }
                }
                else
                {
                    if (LeftPanel != null)
                    {
                        LeftPanel.HorizontalAlignment = HorizontalAlignment.Left;
                        LeftPanel.VerticalAlignment = VerticalAlignment.Center;
                        LeftPanel.Margin = new Thickness(12, 0, 0, 0);
                    }
                    if (CenterPanel != null)
                    {
                        CenterPanel.HorizontalAlignment = HorizontalAlignment.Center;
                        CenterPanel.VerticalAlignment = VerticalAlignment.Center;
                        CenterPanel.Margin = new Thickness(0);
                    }
                    if (RightPanel != null)
                    {
                        RightPanel.HorizontalAlignment = HorizontalAlignment.Right;
                        RightPanel.VerticalAlignment = VerticalAlignment.Center;
                        RightPanel.Margin = new Thickness(0, 0, 12, 0);
                    }
                }

                if (IsPrimaryMonitor)
                {
                    Win32Helper.SetNativeTaskbarPosition(position);
                }

                if (_appWindow.IsVisible)
                {
                    ShowDock();
                }

                if (TaskbarBorder != null)
                {
                    switch (PositionAnimationStyle)
                    {
                        case "BackEase":
                            FactoryAnimation.AnimatePositionBackEase(TaskbarBorder, 0, 30);
                            break;
                        case "Exponential":
                            FactoryAnimation.AnimatePositionExponential(TaskbarBorder, 0, 30);
                            break;
                        case "Elastic":
                            FactoryAnimation.AnimatePositionElastic(TaskbarBorder, 0, 30);
                            break;
                        case "ScaleMorph":
                            FactoryAnimation.AnimatePositionScaleMorph(TaskbarBorder);
                            break;
                        case "Spring":
                        default:
                            FactoryAnimation.AnimatePositionSpring(TaskbarBorder, 0, 30);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetPosition Error: {ex.Message}");
            }
        }

        public void SetAlignment(string alignment)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(alignment)) alignment = "Center";
                alignment = alignment.Trim();

                bool isMoving = false;
                bool canAnimate = false;
                Point btnPointBefore = default;
                Point panelPointBefore = default;
                UIElement? rootElement = this.Content as UIElement;

                if (BtnStart.Parent is Panel startParentOld)
                {
                    isMoving = (alignment.Equals("Center", StringComparison.OrdinalIgnoreCase) && startParentOld != CenterPanel) ||
                               (!alignment.Equals("Center", StringComparison.OrdinalIgnoreCase) && startParentOld != LeftPanel);

                    canAnimate = isMoving && rootElement != null && BtnStart.IsLoaded;

                    if (canAnimate && rootElement != null)
                    {
                        btnPointBefore = BtnStart.TransformToVisual(rootElement).TransformPoint(new Point(0, 0));
                        panelPointBefore = PinnedAppsPanel.TransformToVisual(rootElement).TransformPoint(new Point(0, 0));
                    }
                }

                if (BtnStart.Parent is Panel startParent) startParent.Children.Remove(BtnStart);
                if (PinnedAppsPanel.Parent is Panel pinnedParent) pinnedParent.Children.Remove(PinnedAppsPanel);

                if (alignment.Equals("Left", StringComparison.OrdinalIgnoreCase))
                {
                    LeftPanel?.Children.Insert(0, BtnStart);
                    LeftPanel?.Children.Add(PinnedAppsPanel);

                    if (IsPrimaryMonitor)
                    {
                        Win32Helper.SetNativeStartMenuAlignment(true);
                    }
                }
                else if (alignment.Equals("Center", StringComparison.OrdinalIgnoreCase))
                {
                    CenterPanel?.Children.Insert(0, BtnStart);
                    CenterPanel?.Children.Add(PinnedAppsPanel);

                    if (IsPrimaryMonitor)
                    {
                        Win32Helper.SetNativeStartMenuAlignment(false);
                    }
                }
                else
                {
                    LeftPanel?.Children.Insert(0, BtnStart);
                    CenterPanel?.Children.Add(PinnedAppsPanel);

                    if (IsPrimaryMonitor)
                    {
                        Win32Helper.SetNativeStartMenuAlignment(true);
                    }
                }

                if (canAnimate && rootElement != null)
                {
                    rootElement.UpdateLayout();

                    var btnPointAfter = BtnStart.TransformToVisual(rootElement).TransformPoint(new Point(0, 0));
                    var panelPointAfter = PinnedAppsPanel.TransformToVisual(rootElement).TransformPoint(new Point(0, 0));

                    FactoryAnimation.AnimateHorizontalSlide(BtnStart, (float)(btnPointBefore.X - btnPointAfter.X));
                    FactoryAnimation.AnimateHorizontalSlide(PinnedAppsPanel, (float)(panelPointBefore.X - panelPointAfter.X));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetAlignment Error: {ex.Message}");
            }
        }
        #endregion

        #region System Status Detectors
        private void StartNetworkListener()
        {
            UpdateNetworkIcon();

            NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
        }

        private void NetworkInformation_NetworkStatusChanged(object sender)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateNetworkIcon();
            });
        }

        private void UpdateNetworkIcon()
        {
            try
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();

                if (profile == null)
                {
                    NetworkIcon.Glyph = "\xEB55";
                }
                else if (profile.IsWlanConnectionProfile)
                {
                    NetworkIcon.Glyph = "\xE704";
                }
                else if (profile.IsWwanConnectionProfile)
                {
                    NetworkIcon.Glyph = "\xE81C";
                }
                else
                {
                    NetworkIcon.Glyph = "\xE839";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch network status: {ex.Message}");
                NetworkIcon.Glyph = "\xE704";
            }
        }

        private void StartPowerListener()
        {
            UpdateBatteryIcon();

            PowerManager.BatteryStatusChanged += (s, e) => this.DispatcherQueue.TryEnqueue(UpdateBatteryIcon);
            PowerManager.RemainingChargePercentChanged += (s, e) => this.DispatcherQueue.TryEnqueue(UpdateBatteryIcon);
            PowerManager.EnergySaverStatusChanged += (s, e) => this.DispatcherQueue.TryEnqueue(UpdateBatteryIcon);
        }

        private void UpdateBatteryIcon()
        {
            try
            {
                var status = PowerManager.BatteryStatus;

                if (status == BatteryStatus.NotPresent)
                {
                    BatteryIcon.Visibility = Visibility.Collapsed;
                    return;
                }

                BatteryIcon.Visibility = Visibility.Visible;
                int percent = PowerManager.RemainingChargePercent;
                bool isCharging = status == BatteryStatus.Charging || status == BatteryStatus.Idle;

                int iconIndex = (int)Math.Round(percent / 10.0);
                if (iconIndex < 0) iconIndex = 0;
                if (iconIndex > 10) iconIndex = 10;

                int glyphCode;

                if (isCharging)
                {
                    glyphCode = iconIndex == 10 ? 0xE83E : 0xE85A + iconIndex;
                }
                else if (PowerManager.EnergySaverStatus == EnergySaverStatus.On)
                {
                    glyphCode = iconIndex == 10 ? 0xE86E : 0xE864 + iconIndex;
                }
                else
                {
                    glyphCode = iconIndex == 10 ? 0xE83F : 0xE850 + iconIndex;
                }

                BatteryIcon.Glyph = ((char)glyphCode).ToString();

                ToolTipService.SetToolTip(BatteryIcon, $"Battery: {percent}%");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch battery status: {ex.Message}");
                BatteryIcon.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Functionality Handlers
        private void ClockTimer_Tick(object? sender, object e)
        {
            try
            {
                UpdateClock();
                UpdateAppIndicators();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ClockTimer Critical Error Ignored: {ex.Message}");
            }
        }

        private void UpdateClock()
        {
            ClockText.Text = ShowSeconds ? DateTime.Now.ToLongTimeString() : DateTime.Now.ToShortTimeString();
            DateText.Text = DateTime.Now.ToShortDateString();
        }

        private async void BtnClock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsPrimaryMonitor) return;

                await Win32Helper.ToggleCalendarAsync();
            }
            catch { }
        }

        private async void BtnQuickSettings_Click(object sender, RoutedEventArgs e)
        {
            try { await Win32Helper.ToggleQuickSettingsAsync(); } catch { }
        }

        private async void BtnTrayOverflow_Click(object sender, RoutedEventArgs e)
        {
            try { await Win32Helper.OpenTrayOverflowAsync(); } catch { }
        }
        #endregion
    }
}