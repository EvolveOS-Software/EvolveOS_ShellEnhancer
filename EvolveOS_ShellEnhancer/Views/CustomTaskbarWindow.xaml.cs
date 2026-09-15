// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class CustomTaskbarWindow : Window
    {
        #region P/Invoke Definitions
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        private const uint GW_OWNER = 4;
        #endregion

        #region Fields & Properties
        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private string _currentStyle = "Standard";
        private DispatcherTimer _clockTimer;

        private readonly LivePreviewWindow _previewWindow;

        private readonly List<(string processName, Rectangle indicator, Image backIcon)> _appIndicators = new();

        private Border? _activeDraggedCard = null;
        private Point _dragStartPoint;
        private bool _isTrackingDrag = false;

        public static bool ShowSeconds = false;
        #endregion

        #region Initialization
        public CustomTaskbarWindow()
        {
            this.InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

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

            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
            UpdateClock();

            LoadPinnedAppsAsync();
        }
        #endregion

        #region Process & Window Management
        private List<IntPtr> GetAppWindowHandles(string processName)
        {
            List<IntPtr> handles = new List<IntPtr>();
            bool isExplorer = processName.Equals("explorer", StringComparison.OrdinalIgnoreCase);

            HashSet<uint> targetPids = new HashSet<uint>();
            if (!isExplorer)
            {
                var procs = Process.GetProcessesByName(processName);
                if (procs.Length == 0) return handles;
                foreach (var p in procs) targetPids.Add((uint)p.Id);
            }

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero) return true;

                if (isExplorer)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
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

                item.indicator.Visibility = (handles.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        #endregion

        #region UI Layout & Styling Handlers
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App currentApp)
            {
                currentApp.ToggleStartMenu();
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

        public void SetAlignment(string alignment)
        {
            if (BtnStart.Parent is Panel startParent) startParent.Children.Remove(BtnStart);
            if (PinnedAppsPanel.Parent is Panel pinnedParent) pinnedParent.Children.Remove(PinnedAppsPanel);

            if (alignment == "Left")
            {
                LeftPanel.Children.Insert(0, BtnStart);
                LeftPanel.Children.Add(PinnedAppsPanel);
                Win32Helper.SetNativeStartMenuAlignment(true);
            }
            else if (alignment == "Center")
            {
                CenterPanel.Children.Insert(0, BtnStart);
                CenterPanel.Children.Add(PinnedAppsPanel);
                Win32Helper.SetNativeStartMenuAlignment(false);
            }
            else
            {
                LeftPanel.Children.Insert(0, BtnStart);
                CenterPanel.Children.Add(PinnedAppsPanel);
                Win32Helper.SetNativeStartMenuAlignment(true);
            }
        }
        #endregion

        #region Shortcut Parsing
        public static List<string> GetPinnedTaskbarApps()
        {
            List<string> pinnedApps = new List<string>();

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

            return pinnedApps;
        }

        public static string ParseShortcut(string lnkPath)
        {
            try
            {
                IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(lnkPath);

                return shortcut.TargetPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Shortcut Parse Error: " + ex.Message);
                return string.Empty;
            }
        }
        #endregion

        #region App Loading & Icon Extraction
        private async void LoadPinnedAppsAsync()
        {
            PinnedAppsPanel.Children.Clear();
            _appIndicators.Clear();

            List<string> shortcuts = GetPinnedTaskbarApps();

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

            foreach (string lnk in shortcuts)
            {
                if (!File.Exists(lnk)) continue;

                string targetExe = ParseShortcut(lnk);
                if (!string.IsNullOrWhiteSpace(targetExe))
                {
                    targetExe = Environment.ExpandEnvironmentVariables(targetExe);
                }

                bool isModernApp = string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe);
                string processName = isModernApp ? System.IO.Path.GetFileNameWithoutExtension(lnk) : System.IO.Path.GetFileNameWithoutExtension(targetExe);

                string shortcutName = System.IO.Path.GetFileNameWithoutExtension(lnk);
                if (shortcutName.Equals("File Explorer", StringComparison.OrdinalIgnoreCase) ||
                    shortcutName.Equals("Windows Explorer", StringComparison.OrdinalIgnoreCase))
                {
                    processName = "explorer";
                }

                Grid iconGrid = new Grid();

                Grid iconContainer = new Grid
                {
                    Width = 32,
                    Height = 32,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Image backIcon = new Image
                {
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, -6, 0, 0),
                    Opacity = 0.5,
                    Visibility = Visibility.Collapsed
                };

                Image appIcon = new Image
                {
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 0)
                };

                iconContainer.Children.Add(backIcon);
                iconContainer.Children.Add(appIcon);

                Rectangle indicator = new Rectangle
                {
                    Width = 17,
                    Height = 3,
                    RadiusX = 1.5,
                    RadiusY = 1.5,
                    Fill = new SolidColorBrush(Colors.LightGray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 0),
                    Visibility = Visibility.Collapsed
                };

                iconGrid.Children.Add(iconContainer);
                iconGrid.Children.Add(indicator);

                Border appCard = new Border
                {
                    Width = 40,
                    Height = 40,
                    Padding = new Thickness(0),
                    Margin = new Thickness(2, 0, 2, 0),
                    Background = new SolidColorBrush(Colors.Transparent),
                    CornerRadius = new CornerRadius(4),
                    Child = iconGrid,
                    Tag = lnk
                };

                ToolTipService.SetToolTip(appCard, System.IO.Path.GetFileNameWithoutExtension(lnk));

                _appIndicators.Add((processName, indicator, backIcon));

                appCard.Tapped += (s, e) =>
                {
                    if (_isTrackingDrag) return;
                    _previewWindow.HidePreview();

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

                    if (!activatedExisting)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(lnk) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to launch {lnk}: {ex.Message}");
                        }
                    }
                };

                appCard.PointerEntered += (s, e) =>
                {
                    appCard.Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));

                    if (_isTrackingDrag || string.IsNullOrEmpty(processName)) return;

                    var handles = GetAppWindowHandles(processName);

                    if (handles.Count > 1)
                    {
                        backIcon.Visibility = Visibility.Visible;
                        appIcon.Margin = new Thickness(-4, 4, 0, 0);
                    }

                    if (handles.Count > 0)
                    {
                        var transform = appCard.TransformToVisual(null);
                        var localPoint = transform.TransformPoint(new Point(0, 0));

                        int btnScreenX = _appWindow.Position.X + (int)localPoint.X;
                        int taskbarScreenY = _appWindow.Position.Y;

                        _previewWindow.ShowPreviews(handles, btnScreenX, taskbarScreenY, (int)appCard.Width);
                    }
                };

                appCard.PointerExited += (s, e) =>
                {
                    appCard.Background = new SolidColorBrush(Colors.Transparent);

                    backIcon.Visibility = Visibility.Collapsed;
                    appIcon.Margin = new Thickness(0, 0, 0, 0);

                    _previewWindow.StartHideTimer();
                };

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
                                if (element is Border b && b.Tag is string savedLnk)
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

                PinnedAppsPanel.Children.Add(appCard);

                try
                {
                    StorageFile? file = null;
                    bool iconLoaded = false;

                    if (!isModernApp)
                    {
                        try { file = await StorageFile.GetFileFromPathAsync(targetExe); } catch { }
                    }

                    if (file == null)
                    {
                        try { file = await StorageFile.GetFileFromPathAsync(lnk); } catch { }
                    }

                    if (file != null)
                    {
                        var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 32);

                        if (thumbnail == null)
                            thumbnail = await file.GetThumbnailAsync(ThumbnailMode.ListView, 32);

                        if (thumbnail != null)
                        {
                            var bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(thumbnail);
                            appIcon.Source = bitmapImage;
                            backIcon.Source = bitmapImage;
                            iconLoaded = true;
                        }
                    }

                    if (!iconLoaded && shortcutName.Contains("Explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string explorerPath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\explorer.exe");
                            var icon = System.Drawing.Icon.ExtractAssociatedIcon(explorerPath);

                            if (icon != null)
                            {
                                using var bmp = icon.ToBitmap();
                                using var ms = new MemoryStream();
                                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                ms.Position = 0;

                                var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                                using (var writer = new Windows.Storage.Streams.DataWriter(ras.GetOutputStreamAt(0)))
                                {
                                    writer.WriteBytes(ms.ToArray());
                                    await writer.StoreAsync();
                                }

                                var bitmapImage = new BitmapImage();
                                await bitmapImage.SetSourceAsync(ras);
                                appIcon.Source = bitmapImage;
                                backIcon.Source = bitmapImage;
                            }
                        }
                        catch (Exception fallbackEx)
                        {
                            Debug.WriteLine($"Fallback Explorer icon load error: {fallbackEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Icon load error for {lnk}: {ex.Message}");
                }
            }

            UpdateAppIndicators();
        }
        #endregion

        #region Dock Visibility
        public void ShowDock()
        {
            Win32Helper.HideNativeTaskbar();

            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            int screenWidth = displayArea.OuterBounds.Width;
            int taskbarHeight = 48;

            if (_currentStyle == "Floating")
            {
                Win32Helper.SetCornerPreference(_hWnd, Win32Helper.DWMWCP_ROUNDSMALL);
                TaskbarBorder.CornerRadius = new CornerRadius(4);

                int margin = 5;
                int floatingWidth = screenWidth - (margin * 2);
                int x = displayArea.OuterBounds.X + margin;
                int y = displayArea.OuterBounds.Y + displayArea.OuterBounds.Height - taskbarHeight - margin;

                _appWindow.MoveAndResize(new RectInt32(x, y, floatingWidth, taskbarHeight));
            }
            else
            {
                Win32Helper.SetCornerPreference(_hWnd, Win32Helper.DWMWCP_DONOTROUND);
                TaskbarBorder.CornerRadius = new CornerRadius(0);

                int x = displayArea.OuterBounds.X;
                int y = displayArea.OuterBounds.Y + displayArea.OuterBounds.Height - taskbarHeight;

                _appWindow.MoveAndResize(new RectInt32(x, y, screenWidth, taskbarHeight));
            }

            _appWindow.Show();
            TaskbarOverlayManager.EnsureTopmost(_hWnd);
        }

        public void HideDock()
        {
            _appWindow.Hide();
            Win32Helper.ShowNativeTaskbar();
        }
        #endregion

        #region Functionality Handlers
        private void ClockTimer_Tick(object? sender, object e)
        {
            UpdateClock();
            UpdateAppIndicators();
        }

        private void UpdateClock()
        {
            ClockText.Text = ShowSeconds ? DateTime.Now.ToLongTimeString() : DateTime.Now.ToShortTimeString();
            DateText.Text = DateTime.Now.ToShortDateString();
        }

        private async void BtnClock_Click(object sender, RoutedEventArgs e)
        {
            await Win32Helper.ToggleCalendarAsync();
        }

        private async void BtnQuickSettings_Click(object sender, RoutedEventArgs e)
        {
            await Win32Helper.ToggleQuickSettingsAsync();
        }

        private async void BtnTrayOverflow_Click(object sender, RoutedEventArgs e)
        {
            await Win32Helper.OpenTrayOverflowAsync();
        }
        #endregion
    }
}