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
        public DisplayArea? TargetDisplayArea { get; set; }

        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private bool _isVisible = false;

        private string _currentAlignment = "Center";
        private string _currentPosition = "Bottom";

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

        public void SetPosition(string position)
        {
            _currentPosition = position;
            if (_isVisible)
            {
                ShowMenu();
            }
        }

        /// <summary>
        /// Helper method to target a specific monitor's bounds before displaying.
        /// </summary>
        public void PositionOnDisplay(DisplayArea area)
        {
            TargetDisplayArea = area;
        }

        private void ShowMenu()
        {
            // Use the target display area if provided, otherwise fallback to primary
            var displayArea = TargetDisplayArea ?? DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);

            int menuWidth = (int)MenuContainer.Width;
            int menuHeight = (int)MenuContainer.Height;
            int taskbarOffset = 60;
            int margin = 16;

            int x = 0;
            int y = 0;

            string targetPos = TaskbarManager.GetPositionForDisplay(displayArea.DisplayId.Value.ToString());

            switch (targetPos)
            {
                case "Top":
                    y = displayArea.OuterBounds.Y + taskbarOffset;
                    x = (_currentAlignment == "Center")
                        ? displayArea.OuterBounds.X + (displayArea.OuterBounds.Width - menuWidth) / 2
                        : displayArea.OuterBounds.X + margin;
                    break;

                case "Left":
                    x = displayArea.OuterBounds.X + taskbarOffset;
                    y = (_currentAlignment == "Center")
                        ? displayArea.OuterBounds.Y + (displayArea.OuterBounds.Height - menuHeight) / 2
                        : displayArea.OuterBounds.Y + margin;
                    break;

                case "Right":
                    x = displayArea.OuterBounds.X + displayArea.OuterBounds.Width - menuWidth - taskbarOffset;
                    y = (_currentAlignment == "Center")
                        ? displayArea.OuterBounds.Y + (displayArea.OuterBounds.Height - menuHeight) / 2
                        : displayArea.OuterBounds.Y + margin;
                    break;

                case "Bottom":
                default:
                    y = displayArea.OuterBounds.Y + displayArea.OuterBounds.Height - menuHeight - taskbarOffset;
                    x = (_currentAlignment == "Center")
                        ? displayArea.OuterBounds.X + (displayArea.OuterBounds.Width - menuWidth) / 2
                        : displayArea.OuterBounds.X + margin;
                    break;
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