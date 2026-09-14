// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class CustomTaskbarWindow : Window
    {
        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private string _currentStyle = "Standard";
        private DispatcherTimer _clockTimer;

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
            TaskbarOverlayManager.ApplyWidgetStyles(_hWnd);

            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
            UpdateClock();
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

        public void ShowDock()
        {
            Win32Helper.HideNativeTaskbar();

            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            int screenWidth = displayArea.OuterBounds.Width;
            int taskbarHeight = 48;

            if (_currentStyle == "Floating")
            {
                Win32Helper.SetCornerPreference(_hWnd, Win32Helper.DWMWCP_ROUND);
                TaskbarBorder.CornerRadius = new CornerRadius(4);

                int margin = 10;
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
        }

        private void UpdateClock()
        {
            ClockText.Text = DateTime.Now.ToShortTimeString();
            DateText.Text = DateTime.Now.ToShortDateString();
        }

        private void BtnExplorer_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start("explorer.exe"); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private async void BtnBrowser_Click(object sender, RoutedEventArgs e)
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.google.com")); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private async void BtnNetwork_Click(object sender, RoutedEventArgs e)
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-availablenetworks:")); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void BtnVolume_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start("sndvol.exe"); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        #endregion
    }
}