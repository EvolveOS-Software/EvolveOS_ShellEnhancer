// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Text.RegularExpressions;
using EvolveOS_ShellEnhancer.Enums;
using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class MessageWindow : Window
    {
        private TimerControlManager? _timer = default;

        public MessageWindow(MessageWindowState windowState = MessageWindowState.Warning)
        {
            this.InitializeComponent();

            SettingsEngine.CheckingParameters();

            ConfigureWindow();

            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

            if (this.Content is FrameworkElement rootElement)
            {
                if (rootElement is Panel rootPanel)
                {
                    rootPanel.Background = new SolidColorBrush(Colors.Transparent);
                }
            }

            WarningContent.Visibility = windowState == MessageWindowState.Warning ? Visibility.Visible : Visibility.Collapsed;
            NotSupportContent.Visibility = windowState == MessageWindowState.NotSupported ? Visibility.Visible : Visibility.Collapsed;
            AlreadyRunningContent.Visibility = windowState == MessageWindowState.AlreadyRunning ? Visibility.Visible : Visibility.Collapsed;
            MissingOptimizerContent.Visibility = windowState == MessageWindowState.MissingOptimizer ? Visibility.Visible : Visibility.Collapsed;

            this.Closed += (s, e) =>
            {
                _timer?.Stop();
            };

            _timer = new TimerControlManager(TimeSpan.FromSeconds(4), TimerControlManager.TimerMode.CountDown, time =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    string currentContent = BtnAccept.Content?.ToString() ?? "";
                    BtnAccept.Content = $"{new Regex("[(05)(04)(03)(02)]").Replace(currentContent, "")}({time:ss})";
                });
            }, () =>
            {
                this.DispatcherQueue.TryEnqueue(() => App.ExitApp());
            });

            _timer.Start();
        }

        private void ConfigureWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            int style = Win32Helper.GetWindowLong(hwnd, Win32Helper.GWL_STYLE);
            Win32Helper.SetWindowLong(hwnd, Win32Helper.GWL_STYLE, style & ~Win32Helper.WS_CAPTION & ~Win32Helper.WS_THICKFRAME);

            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
                if (appWindow.TitleBar != null) appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            }

            int width = 400; int height = 200;
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
                int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(centeredX, centeredY, width, height));
            }
        }

        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(sender as UIElement);
            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                Win32Helper.SendMessage(hwnd, 0x00A1, (IntPtr)2, IntPtr.Zero);
            }
        }

        private void BtnAccept_Click(object sender, RoutedEventArgs e) => App.ExitApp();
    }
}