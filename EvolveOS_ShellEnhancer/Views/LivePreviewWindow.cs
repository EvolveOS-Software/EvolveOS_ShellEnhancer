// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities.Helpers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public class LivePreviewWindow : Window
    {
        #region P/Invokes and Fields
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint WM_CLOSE = 0x0010;

        private readonly IntPtr _hWnd;
        private readonly AppWindow _appWindow;

        private List<IntPtr> _thumbHandles = new();
        private List<IntPtr> _currentSourceHwnds = new();

        private DispatcherTimer _hideTimer;
        private StackPanel _rootStackPanel;

        public static bool EnableActionButtons { get; set; } = true;

        private const int ThumbWidth = 220;
        private const int ThumbHeight = 142;
        private const int ActionPanelHeight = 40;
        private const int SlotMargin = 10;
        private const int HighlightPaddingX = 5;
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

            _rootStackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Colors.Transparent),
                Padding = new Thickness(SlotMargin),
                Spacing = SlotMargin
            };

            this.Content = _rootStackPanel;

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                HidePreview();
            };

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, ThumbWidth, ThumbHeight));
            _appWindow.Show();
        }
        #endregion

        #region Preview Methods
        public void ShowPreviews(List<IntPtr> sourceHwnds, int buttonScreenX, int taskbarScreenY, int buttonWidth)
        {
            if (this.DispatcherQueue.HasThreadAccess)
            {
                ExecuteShow(sourceHwnds, buttonScreenX, taskbarScreenY, buttonWidth);
            }
            else
            {
                this.DispatcherQueue.TryEnqueue(() => ExecuteShow(sourceHwnds, buttonScreenX, taskbarScreenY, buttonWidth));
            }
        }

        private void ExecuteShow(List<IntPtr> sourceHwnds, int buttonScreenX, int taskbarScreenY, int buttonWidth)
        {
            _hideTimer.Stop();

            foreach (var thumb in _thumbHandles)
            {
                Win32Helper.DwmUnregisterThumbnail(thumb);
            }
            _thumbHandles.Clear();
            _currentSourceHwnds.Clear();
            _rootStackPanel.Children.Clear();

            if (sourceHwnds == null || sourceHwnds.Count == 0) return;

            _currentSourceHwnds.AddRange(sourceHwnds);

            int itemWidth = ThumbWidth + (HighlightPaddingX * 2);
            int itemHeight = ThumbHeight + ActionPanelHeight;
            int totalWidth = SlotMargin + (sourceHwnds.Count * itemWidth) + ((sourceHwnds.Count - 1) * SlotMargin) + SlotMargin;
            int totalHeight = SlotMargin + itemHeight + SlotMargin;

            int x = buttonScreenX - (totalWidth / 2) + (buttonWidth / 2);
            int y = taskbarScreenY - totalHeight - SlotMargin;

            if (x < 10) x = 10;

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, totalWidth, totalHeight));
            SetWindowPos(_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            IntPtr currentHwnd = WindowNative.GetWindowHandle(this);

            for (int i = 0; i < sourceHwnds.Count; i++)
            {
                IntPtr targetHwnd = sourceHwnds[i];

                Grid slotGrid = new Grid
                {
                    Width = itemWidth,
                    Height = itemHeight,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Colors.Transparent)
                };

                System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                GetWindowText(targetHwnd, sb, 256);
                string windowTitle = sb.ToString();
                if (string.IsNullOrWhiteSpace(windowTitle)) windowTitle = "Application";

                TextBlock titleBlock = new TextBlock
                {
                    Text = windowTitle,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(HighlightPaddingX, 3, 12, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 1
                };

                StackPanel actionPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 8),
                    Spacing = 16,
                    Visibility = Visibility.Visible
                };

                var closeBtn = new Button { Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new FontIcon { Glyph = "\uE8BB", FontSize = 12 }, new TextBlock { Text = "Close", FontSize = 12 } } }, Height = 28, Padding = new Thickness(8, 0, 8, 0), CornerRadius = new CornerRadius(4) };
                var killBtn = new Button { Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new FontIcon { Glyph = "\uE74D", FontSize = 12 }, new TextBlock { Text = "Kill", FontSize = 12 } } }, Height = 28, Padding = new Thickness(8, 0, 8, 0), CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(70, 255, 0, 0)) };

                killBtn.Visibility = EnableActionButtons ? Visibility.Visible : Visibility.Collapsed;

                closeBtn.Click += (s, e) => CloseWindow(targetHwnd);
                killBtn.Click += (s, e) => KillProcess(targetHwnd);

                actionPanel.Children.Add(closeBtn);
                actionPanel.Children.Add(killBtn);

                slotGrid.Children.Add(titleBlock);
                slotGrid.Children.Add(actionPanel);

                slotGrid.PointerEntered += (s, e) =>
                {
                    _hideTimer.Stop();
                    slotGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255));

                    if (Win32Helper.IsIconic(targetHwnd)) Win32Helper.ShowWindow(targetHwnd, Win32Helper.SW_RESTORE);
                    Win32Helper.SetForegroundWindow(targetHwnd);
                    SetWindowPos(_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                };

                slotGrid.PointerExited += (s, e) =>
                {
                    slotGrid.Background = new SolidColorBrush(Colors.Transparent);
                    StartHideTimer();
                };

                slotGrid.PointerReleased += (s, e) =>
                {
                    try { slotGrid.ReleasePointerCaptures(); } catch { }
                    if (Win32Helper.IsIconic(targetHwnd)) Win32Helper.ShowWindow(targetHwnd, Win32Helper.SW_RESTORE);
                    Win32Helper.SetForegroundWindow(targetHwnd);
                    this.DispatcherQueue.TryEnqueue(() => ExecuteHide());
                };

                _rootStackPanel.Children.Add(slotGrid);

                int hr = Win32Helper.DwmRegisterThumbnail(currentHwnd, targetHwnd, out IntPtr thumbHandle);
                if (hr == 0 && thumbHandle != IntPtr.Zero)
                {
                    _thumbHandles.Add(thumbHandle);

                    int leftOffset = SlotMargin + (i * (itemWidth + SlotMargin)) + HighlightPaddingX;
                    int topOffset = SlotMargin + 24;

                    Win32Helper.DWM_THUMBNAIL_PROPERTIES props = new Win32Helper.DWM_THUMBNAIL_PROPERTIES
                    {
                        dwFlags = Win32Helper.DWM_TNP_VISIBLE | Win32Helper.DWM_TNP_RECTDESTINATION | Win32Helper.DWM_TNP_OPACITY,
                        fVisible = true,
                        opacity = 255,
                        rcDestination = new Win32Helper.RECT
                        {
                            Left = leftOffset,
                            Top = topOffset,
                            Right = leftOffset + ThumbWidth,
                            Bottom = topOffset + (ThumbHeight - 24)
                        }
                    };

                    Win32Helper.DwmUpdateThumbnailProperties(thumbHandle, ref props);
                }
            }
        }

        private void CloseWindow(IntPtr handle)
        {
            SendMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            ExecuteHide();
        }

        private void KillProcess(IntPtr handle)
        {
            GetWindowThreadProcessId(handle, out uint pid);
            if (pid > 0)
            {
                try
                {
                    Process p = Process.GetProcessById((int)pid);
                    p.Kill();
                }
                catch { }
            }
            ExecuteHide();
        }

        public void StartHideTimer()
        {
            if (this.DispatcherQueue.HasThreadAccess) _hideTimer.Start();
            else this.DispatcherQueue.TryEnqueue(() => _hideTimer.Start());
        }

        public void HidePreview()
        {
            if (this.DispatcherQueue.HasThreadAccess) ExecuteHide();
            else this.DispatcherQueue.TryEnqueue(ExecuteHide);
        }

        private void ExecuteHide()
        {
            _hideTimer.Stop();

            foreach (var thumb in _thumbHandles)
            {
                Win32Helper.DwmUnregisterThumbnail(thumb);
            }
            _thumbHandles.Clear();
            _currentSourceHwnds.Clear();
            _rootStackPanel.Children.Clear();

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, ThumbWidth, ThumbHeight));
        }
        #endregion
    }
}