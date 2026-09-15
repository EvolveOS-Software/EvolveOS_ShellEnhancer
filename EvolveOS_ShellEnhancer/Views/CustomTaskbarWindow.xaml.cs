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
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class CustomTaskbarWindow : Window
    {
        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private string _currentStyle = "Standard";
        private DispatcherTimer _clockTimer;

        private readonly LivePreviewWindow _previewWindow;

        private readonly List<(string targetExe, Rectangle indicator)> _appIndicators = new();

        public CustomTaskbarWindow()
        {
            this.InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
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

        private async void LoadPinnedAppsAsync()
        {
            PinnedAppsPanel.Children.Clear();
            _appIndicators.Clear();

            List<string> shortcuts = GetPinnedTaskbarApps();

            foreach (string lnk in shortcuts)
            {
                string targetExe = ParseShortcut(lnk);

                if (!string.IsNullOrWhiteSpace(targetExe))
                {
                    targetExe = Environment.ExpandEnvironmentVariables(targetExe);

                    if (File.Exists(targetExe))
                    {
                        Grid iconGrid = new Grid();

                        Image appIcon = new Image
                        {
                            Width = 24,
                            Height = 24,
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 0)
                        };

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

                        iconGrid.Children.Add(appIcon);
                        iconGrid.Children.Add(indicator);

                        Button appButton = new Button
                        {
                            Width = 40,
                            Height = 40,
                            MinWidth = 0,
                            MinHeight = 0,
                            Padding = new Thickness(0),
                            Margin = new Thickness(2, 0, 2, 0),
                            Background = new SolidColorBrush(Colors.Transparent),
                            BorderThickness = new Thickness(0),
                            CornerRadius = new CornerRadius(4),

                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            VerticalContentAlignment = VerticalAlignment.Stretch,

                            Content = iconGrid
                        };

                        ToolTipService.SetToolTip(appButton, System.IO.Path.GetFileNameWithoutExtension(targetExe));

                        _appIndicators.Add((targetExe, indicator));

                        appButton.Click += (s, e) =>
                        {
                            _previewWindow.HidePreview();

                            string processName = System.IO.Path.GetFileNameWithoutExtension(targetExe);
                            var runningProcesses = Process.GetProcessesByName(processName);
                            bool activatedExisting = false;

                            foreach (var p in runningProcesses)
                            {
                                if (p.MainWindowHandle != IntPtr.Zero)
                                {
                                    if (Win32Helper.IsIconic(p.MainWindowHandle))
                                    {
                                        Win32Helper.ShowWindow(p.MainWindowHandle, Win32Helper.SW_RESTORE);
                                    }

                                    Win32Helper.SetForegroundWindow(p.MainWindowHandle);
                                    activatedExisting = true;
                                    break;
                                }
                            }

                            if (!activatedExisting)
                            {
                                try
                                {
                                    Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = true });
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Failed to launch {targetExe}: {ex.Message}");
                                }
                            }
                        };

                        appButton.PointerEntered += (s, e) =>
                        {
                            string processName = System.IO.Path.GetFileNameWithoutExtension(targetExe);
                            var runningProcesses = Process.GetProcessesByName(processName);

                            foreach (var p in runningProcesses)
                            {
                                if (p.MainWindowHandle != IntPtr.Zero)
                                {
                                    var transform = appButton.TransformToVisual(null);
                                    var localPoint = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

                                    int btnScreenX = _appWindow.Position.X + (int)localPoint.X;
                                    int taskbarScreenY = _appWindow.Position.Y;

                                    _previewWindow.ShowPreview(p.MainWindowHandle, btnScreenX, taskbarScreenY, (int)appButton.Width);
                                    break;
                                }
                            }
                        };

                        appButton.PointerExited += (s, e) =>
                        {
                            _previewWindow.StartHideTimer();
                        };

                        PinnedAppsPanel.Children.Add(appButton);

                        try
                        {
                            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(targetExe);
                            var thumbnail = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, 32);

                            if (thumbnail != null)
                            {
                                var bitmapImage = new BitmapImage();
                                await bitmapImage.SetSourceAsync(thumbnail);
                                appIcon.Source = bitmapImage;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Icon load error for {targetExe}: {ex.Message}");
                        }
                    }
                }
            }

            UpdateAppIndicators();
        }

        private void UpdateAppIndicators()
        {
            foreach (var item in _appIndicators)
            {
                string processName = System.IO.Path.GetFileNameWithoutExtension(item.targetExe);
                var processes = Process.GetProcessesByName(processName);

                bool isRunning = false;

                foreach (var p in processes)
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        isRunning = true;
                        break;
                    }
                }

                item.indicator.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
            }
        }

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

                _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, floatingWidth, taskbarHeight));
            }
            else
            {
                Win32Helper.SetCornerPreference(_hWnd, Win32Helper.DWMWCP_DONOTROUND);
                TaskbarBorder.CornerRadius = new CornerRadius(0);

                int x = displayArea.OuterBounds.X;
                int y = displayArea.OuterBounds.Y + displayArea.OuterBounds.Height - taskbarHeight;

                _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, screenWidth, taskbarHeight));
            }

            _appWindow.Show();
            TaskbarOverlayManager.EnsureTopmost(_hWnd);
        }

        public void HideDock()
        {
            _appWindow.Hide();
            Win32Helper.ShowNativeTaskbar();
        }

        #region Functionality Handlers

        private void ClockTimer_Tick(object? sender, object e)
        {
            UpdateClock();
            UpdateAppIndicators();
        }

        private void UpdateClock()
        {
            ClockText.Text = DateTime.Now.ToShortTimeString();
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