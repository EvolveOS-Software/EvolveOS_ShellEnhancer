// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities.Helpers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public class LivePreviewWindow : Window
    {
        #region P/Invokes and Fields
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        private readonly IntPtr _hWnd;
        private readonly AppWindow _appWindow;
        private IntPtr _thumbHandle = IntPtr.Zero;

        private IntPtr _currentSourceHwnd = IntPtr.Zero;
        private DispatcherTimer _hideTimer;
        private Grid _contentGrid;

        private const int WindowWidth = 240;
        private const int WindowHeight = 150;
        #endregion

        #region Constructor
        public LivePreviewWindow()
        {
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

            Win32Helper.RemoveWindowBorders(_hWnd);
            Win32Helper.PreventFocusStealing(_hWnd);

            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

            _contentGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.Transparent),
                CornerRadius = new CornerRadius(8)
            };

            _contentGrid.PointerEntered += ContentGrid_PointerEntered;
            _contentGrid.PointerExited += ContentGrid_PointerExited;
            _contentGrid.PointerReleased += ContentGrid_PointerReleased;

            this.Content = _contentGrid;

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                HidePreview();
            };

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, WindowWidth, WindowHeight));
            _appWindow.Show();
        }
        #endregion

        #region Event Handlers
        private void ContentGrid_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _hideTimer.Stop();

            _contentGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255));

            if (_currentSourceHwnd != IntPtr.Zero)
            {
                if (Win32Helper.IsIconic(_currentSourceHwnd))
                {
                    Win32Helper.ShowWindow(_currentSourceHwnd, Win32Helper.SW_RESTORE);
                }
                Win32Helper.SetForegroundWindow(_currentSourceHwnd);

                SetWindowPos(_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        private void ContentGrid_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _contentGrid.Background = new SolidColorBrush(Colors.Transparent);
            StartHideTimer();
        }

        private void ContentGrid_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_currentSourceHwnd != IntPtr.Zero)
            {
                IntPtr targetHwnd = _currentSourceHwnd;

                try { _contentGrid.ReleasePointerCaptures(); } catch { }

                if (Win32Helper.IsIconic(targetHwnd))
                {
                    Win32Helper.ShowWindow(targetHwnd, Win32Helper.SW_RESTORE);
                }
                Win32Helper.SetForegroundWindow(targetHwnd);

                this.DispatcherQueue.TryEnqueue(() => ExecuteHide());
            }
        }
        #endregion

        #region Preview Methods
        public void ShowPreview(IntPtr sourceHwnd, int buttonScreenX, int taskbarScreenY, int buttonWidth)
        {
            if (this.DispatcherQueue.HasThreadAccess)
            {
                ExecuteShow(sourceHwnd, buttonScreenX, taskbarScreenY, buttonWidth);
            }
            else
            {
                this.DispatcherQueue.TryEnqueue(() => ExecuteShow(sourceHwnd, buttonScreenX, taskbarScreenY, buttonWidth));
            }
        }

        private void ExecuteShow(IntPtr sourceHwnd, int buttonScreenX, int taskbarScreenY, int buttonWidth)
        {
            _hideTimer.Stop();

            IntPtr currentHwnd = WindowNative.GetWindowHandle(this);

            if (_thumbHandle != IntPtr.Zero)
            {
                Win32Helper.DwmUnregisterThumbnail(_thumbHandle);
                _thumbHandle = IntPtr.Zero;
            }

            _currentSourceHwnd = sourceHwnd;

            int margin = 10;
            int x = buttonScreenX - (WindowWidth / 2) + (buttonWidth / 2);
            int y = taskbarScreenY - WindowHeight - margin;

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, WindowWidth, WindowHeight));
            SetWindowPos(_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            if (sourceHwnd != IntPtr.Zero)
            {
                int hr = Win32Helper.DwmRegisterThumbnail(currentHwnd, sourceHwnd, out _thumbHandle);
                if (hr == 0 && _thumbHandle != IntPtr.Zero)
                {
                    int thumbWidth = 220;
                    int thumbHeight = 118;
                    int leftOffset = (WindowWidth - thumbWidth) / 2;
                    int topOffset = 12;

                    Win32Helper.DWM_THUMBNAIL_PROPERTIES props = new Win32Helper.DWM_THUMBNAIL_PROPERTIES
                    {
                        dwFlags = Win32Helper.DWM_TNP_VISIBLE | Win32Helper.DWM_TNP_RECTDESTINATION | Win32Helper.DWM_TNP_OPACITY,
                        fVisible = true,
                        opacity = 255,
                        rcDestination = new Win32Helper.RECT
                        {
                            Left = leftOffset,
                            Top = topOffset,
                            Right = leftOffset + thumbWidth,
                            Bottom = topOffset + thumbHeight
                        }
                    };

                    Win32Helper.DwmUpdateThumbnailProperties(_thumbHandle, ref props);
                }
            }
        }

        public void StartHideTimer()
        {
            if (this.DispatcherQueue.HasThreadAccess)
            {
                _hideTimer.Start();
            }
            else
            {
                this.DispatcherQueue.TryEnqueue(() => _hideTimer.Start());
            }
        }

        public void HidePreview()
        {
            if (this.DispatcherQueue.HasThreadAccess)
            {
                ExecuteHide();
            }
            else
            {
                this.DispatcherQueue.TryEnqueue(ExecuteHide);
            }
        }

        private void ExecuteHide()
        {
            _hideTimer.Stop();

            if (_thumbHandle != IntPtr.Zero)
            {
                Win32Helper.DwmUnregisterThumbnail(_thumbHandle);
                _thumbHandle = IntPtr.Zero;
            }

            _currentSourceHwnd = IntPtr.Zero;
            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, WindowWidth, WindowHeight));
        }
        #endregion
    }
}