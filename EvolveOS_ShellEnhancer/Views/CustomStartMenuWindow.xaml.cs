// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class CustomStartMenuWindow : Window
    {
        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private bool _isVisible = false;

        private string _currentAlignment = "Center";

        public CustomStartMenuWindow()
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

            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            Win32Helper.RemoveWindowBorders(_hWnd);
            TaskbarOverlayManager.ApplyWidgetStyles(_hWnd);

            _appWindow.Hide();

            this.Activated += OnWindowActivated;
        }

        public void ToggleVisibility()
        {
            if (_isVisible)
            {
                HideMenu();
            }
            else
            {
                ShowMenu();
            }
        }

        public void SetAlignment(string alignment)
        {
            _currentAlignment = alignment;

            if (_isVisible)
            {
                ShowMenu();
            }
        }

        private void ShowMenu()
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);

            int menuWidth = (int)MenuContainer.Width;
            int menuHeight = (int)MenuContainer.Height;
            int taskbarOffset = 60;

            int leftMargin = 16;

            int x;
            int y = displayArea.OuterBounds.Y + displayArea.OuterBounds.Height - menuHeight - taskbarOffset;

            if (_currentAlignment == "Center")
            {
                x = displayArea.OuterBounds.X + (displayArea.OuterBounds.Width - menuWidth) / 2;
            }
            else
            {
                x = displayArea.OuterBounds.X + leftMargin;
            }

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, menuWidth, menuHeight));

            _appWindow.Show();
            _isVisible = true;

            TaskbarOverlayManager.EnsureTopmost(_hWnd);
        }

        private void HideMenu()
        {
            _appWindow.Hide();
            _isVisible = false;

            App.LastStartMenuCloseTime = DateTime.Now;
        }

        public void SetStyle(string style)
        {
            if (style == "Compact")
            {
                MenuContainer.Width = 400;
                MenuContainer.Height = 550;
            }
            else if (style == "Standard")
            {
                MenuContainer.Width = 600;
                MenuContainer.Height = 700;
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                HideMenu();
            }
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            HideMenu();
        }
    }
}