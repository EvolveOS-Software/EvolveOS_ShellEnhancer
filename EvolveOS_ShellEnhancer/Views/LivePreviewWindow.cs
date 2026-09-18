// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using Microsoft.UI;
using Microsoft.UI.Text;
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
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll", EntryPoint = "#113")]
        private static extern int DwmpActivateLivePreview(uint enable, IntPtr hWnd, IntPtr top, uint peekType);

        private const int DWMWA_EXCLUDED_FROM_PEEK = 12;

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

        private readonly SolidColorBrush _hoverBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255));
        private readonly SolidColorBrush _transparentBrush = new SolidColorBrush(Colors.Transparent);

        private DispatcherTimer _peekTimer;
        private IntPtr _peekingHwnd = IntPtr.Zero;

        private bool _isPeekActive = false;
        private bool _isRedrawing = false;

        private int _lastCardScreenX;
        private int _lastCardScreenY;
        private int _lastCardWidth;
        private int _lastCardHeight;
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

            int exclude = 1;
            DwmSetWindowAttribute(_hWnd, DWMWA_EXCLUDED_FROM_PEEK, ref exclude, sizeof(int));

            _rootStackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = _transparentBrush,
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

            _peekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _peekTimer.Tick += (s, e) =>
            {
                _peekTimer.Stop();

                if (_peekingHwnd != IntPtr.Zero && IsWindow(_peekingHwnd))
                {
                    SafeToggleAeroPeek(true, _peekingHwnd);
                }
            };

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, ThumbWidth, ThumbHeight));
            _appWindow.Show();
        }
        #endregion

        #region Preview Methods
        public void ShowPreviews(List<IntPtr> sourceHwnds, int cardScreenX, int cardScreenY, int cardWidth, int cardHeight)
        {
            Win32Helper.HideNativeTaskbar();

            if (this.DispatcherQueue.HasThreadAccess)
            {
                ExecuteShow(sourceHwnds, cardScreenX, cardScreenY, cardWidth, cardHeight);
            }
            else
            {
                this.DispatcherQueue.TryEnqueue(() => ExecuteShow(sourceHwnds, cardScreenX, cardScreenY, cardWidth, cardHeight));
            }
        }

        private void ExecuteShow(List<IntPtr> sourceHwnds, int cardScreenX, int cardScreenY, int cardWidth, int cardHeight)
        {
            _isRedrawing = true;
            try
            {
                _hideTimer.Stop();
                _peekTimer.Stop();

                if (_peekingHwnd != IntPtr.Zero)
                {
                    SafeToggleAeroPeek(false, _peekingHwnd);
                    _peekingHwnd = IntPtr.Zero;
                }

                _lastCardScreenX = cardScreenX;
                _lastCardScreenY = cardScreenY;
                _lastCardWidth = cardWidth;
                _lastCardHeight = cardHeight;

                double smallFontSize = 12;
                if (Application.Current.Resources.TryGetValue("AppFontSizeSmall", out var sizeRes) && sizeRes is double s)
                {
                    smallFontSize = s;
                }

                FontFamily customFont = new FontFamily("Segoe UI");
                if (Application.Current.Resources.TryGetValue("AppCustomFont", out var fontRes) && fontRes is FontFamily f)
                {
                    customFont = f;
                }

                foreach (var thumb in _thumbHandles)
                {
                    Win32Helper.DwmUnregisterThumbnail(thumb);
                }
                _thumbHandles.Clear();
                _currentSourceHwnds.Clear();
                _rootStackPanel.Children.Clear();

                if (sourceHwnds == null || sourceHwnds.Count == 0) return;

                _currentSourceHwnds.AddRange(sourceHwnds);

                string position = SettingsEngine.Shell_TaskbarPosition;
                bool isVertical = (position == "Left" || position == "Right");

                _rootStackPanel.Orientation = isVertical ? Orientation.Vertical : Orientation.Horizontal;

                int itemWidth = ThumbWidth + (HighlightPaddingX * 2);
                int itemHeight = ThumbHeight + ActionPanelHeight;

                int totalWidth, totalHeight;
                if (isVertical)
                {
                    totalWidth = SlotMargin + itemWidth + SlotMargin;
                    totalHeight = SlotMargin + (sourceHwnds.Count * itemHeight) + ((sourceHwnds.Count - 1) * SlotMargin) + SlotMargin;
                }
                else
                {
                    totalWidth = SlotMargin + (sourceHwnds.Count * itemWidth) + ((sourceHwnds.Count - 1) * SlotMargin) + SlotMargin;
                    totalHeight = SlotMargin + itemHeight + SlotMargin;
                }

                int x, y;

                var point = new Windows.Graphics.PointInt32(cardScreenX, cardScreenY);
                var displayArea = DisplayArea.GetFromPoint(point, DisplayAreaFallback.Nearest);

                int screenLeft = displayArea.OuterBounds.X;
                int screenTop = displayArea.OuterBounds.Y;
                int screenRight = screenLeft + displayArea.OuterBounds.Width;
                int screenBottom = screenTop + displayArea.OuterBounds.Height;

                switch (position)
                {
                    case "Top":
                        x = cardScreenX - (totalWidth / 2) + (cardWidth / 2);
                        y = cardScreenY + cardHeight + SlotMargin;
                        break;
                    case "Left":
                        x = cardScreenX + cardWidth + SlotMargin;
                        y = cardScreenY - (totalHeight / 2) + (cardHeight / 2);
                        break;
                    case "Right":
                        x = cardScreenX - totalWidth - SlotMargin;
                        y = cardScreenY - (totalHeight / 2) + (cardHeight / 2);
                        break;
                    case "Bottom":
                    default:
                        x = cardScreenX - (totalWidth / 2) + (cardWidth / 2);
                        y = cardScreenY - totalHeight - SlotMargin;
                        break;
                }

                if (x < screenLeft + 10) x = screenLeft + 10;
                if (x + totalWidth > screenRight - 10) x = screenRight - totalWidth - 10;
                if (y < screenTop + 10) y = screenTop + 10;
                if (y + totalHeight > screenBottom - 10) y = screenBottom - totalHeight - 10;

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
                        Background = _transparentBrush
                    };

                    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                    GetWindowText(targetHwnd, sb, 256);
                    string windowTitle = sb.ToString();
                    if (string.IsNullOrWhiteSpace(windowTitle)) windowTitle = "Application";

                    TextBlock titleBlock = new TextBlock
                    {
                        Text = windowTitle,
                        FontSize = smallFontSize,
                        FontFamily = customFont,
                        FontWeight = FontWeights.SemiBold,
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

                    var closeBtn = new Button
                    {
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            Children = {
                                new FontIcon { Glyph = "\uE8BB", FontSize = 12 },
                                new TextBlock { Text = "Close", FontSize = smallFontSize, FontFamily = customFont }
                            }
                        },
                        Height = 28,
                        Padding = new Thickness(8, 0, 8, 0),
                        CornerRadius = new CornerRadius(4)
                    };

                    var killBtn = new Button
                    {
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            Children = {
                                new FontIcon { Glyph = "\uE74D", FontSize = 12 },
                                new TextBlock { Text = "Kill", FontSize = smallFontSize, FontFamily = customFont }
                            }
                        },
                        Height = 28,
                        Padding = new Thickness(8, 0, 8, 0),
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(70, 255, 0, 0))
                    };

                    killBtn.Visibility = EnableActionButtons ? Visibility.Visible : Visibility.Collapsed;

                    closeBtn.Click += (s, e) => CloseWindow(targetHwnd);
                    killBtn.Click += (s, e) => KillProcess(targetHwnd);

                    actionPanel.Children.Add(closeBtn);
                    actionPanel.Children.Add(killBtn);

                    slotGrid.Children.Add(titleBlock);
                    slotGrid.Children.Add(actionPanel);

                    slotGrid.PointerEntered += (s, e) =>
                    {
                        if (_isRedrawing) return;

                        _hideTimer.Stop();
                        slotGrid.Background = _hoverBrush;

                        if (_peekingHwnd != IntPtr.Zero && _peekingHwnd != targetHwnd)
                        {
                            SafeToggleAeroPeek(false, _peekingHwnd);
                        }

                        _peekingHwnd = targetHwnd;
                        _peekTimer.Start();
                    };

                    slotGrid.PointerExited += (s, e) =>
                    {
                        if (_isRedrawing) return;

                        slotGrid.Background = _transparentBrush;

                        _peekTimer.Stop();

                        if (_peekingHwnd != IntPtr.Zero)
                        {
                            SafeToggleAeroPeek(false, _peekingHwnd);
                            _peekingHwnd = IntPtr.Zero;
                        }

                        StartHideTimer();
                    };

                    slotGrid.PointerReleased += (s, e) =>
                    {
                        try { slotGrid.ReleasePointerCaptures(); } catch { }

                        _peekTimer.Stop();
                        if (_peekingHwnd != IntPtr.Zero)
                        {
                            SafeToggleAeroPeek(false, _peekingHwnd);
                            _peekingHwnd = IntPtr.Zero;
                        }

                        if (Win32Helper.IsIconic(targetHwnd)) Win32Helper.ShowWindow(targetHwnd, Win32Helper.SW_RESTORE);
                        Win32Helper.SetForegroundWindow(targetHwnd);
                        this.DispatcherQueue.TryEnqueue(() => ExecuteHide());
                    };

                    _rootStackPanel.Children.Add(slotGrid);

                    int hr = Win32Helper.DwmRegisterThumbnail(currentHwnd, targetHwnd, out IntPtr thumbHandle);
                    if (hr == 0 && thumbHandle != IntPtr.Zero)
                    {
                        _thumbHandles.Add(thumbHandle);

                        int leftOffset, topOffset;
                        if (isVertical)
                        {
                            leftOffset = SlotMargin + HighlightPaddingX;
                            topOffset = SlotMargin + (i * (itemHeight + SlotMargin)) + 24;
                        }
                        else
                        {
                            leftOffset = SlotMargin + (i * (itemWidth + SlotMargin)) + HighlightPaddingX;
                            topOffset = SlotMargin + 24;
                        }

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
            finally
            {
                _isRedrawing = false;
            }
        }

        private void SafeToggleAeroPeek(bool enable, IntPtr targetHwnd)
        {
            try
            {
                if (enable)
                {
                    if (targetHwnd == IntPtr.Zero || !IsWindow(targetHwnd)) return;

                    SetWindowPos(_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    DwmpActivateLivePreview(1, targetHwnd, _hWnd, 1);
                    _isPeekActive = true;
                }
                else
                {
                    if (_isPeekActive)
                    {
                        IntPtr safeHandle = IsWindow(targetHwnd) ? targetHwnd : IntPtr.Zero;
                        DwmpActivateLivePreview(0, safeHandle, IntPtr.Zero, 1);
                        _isPeekActive = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SafeToggleAeroPeek intercepted a DWM fault: {ex.Message}");
            }
        }

        private void CloseWindow(IntPtr handle)
        {
            if (_peekingHwnd == handle || _isPeekActive)
            {
                SafeToggleAeroPeek(false, handle);
                _peekingHwnd = IntPtr.Zero;
            }
            _peekTimer.Stop();
            _hideTimer.Stop();

            PostMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            _currentSourceHwnds.Remove(handle);

            var remainingWindows = new List<IntPtr>(_currentSourceHwnds);

            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (remainingWindows.Count > 0)
                {
                    ExecuteShow(remainingWindows, _lastCardScreenX, _lastCardScreenY, _lastCardWidth, _lastCardHeight);
                }
                else
                {
                    ExecuteHide();
                }
            });
        }

        private void KillProcess(IntPtr handle)
        {
            if (_peekingHwnd == handle || _isPeekActive)
            {
                SafeToggleAeroPeek(false, handle);
                _peekingHwnd = IntPtr.Zero;
            }
            _peekTimer.Stop();
            _hideTimer.Stop();

            GetWindowThreadProcessId(handle, out uint pid);
            if (pid > 0)
            {
                try
                {
                    Process p = Process.GetProcessById((int)pid);
                    p.Kill();
                }
                catch { }

                _currentSourceHwnds.RemoveAll(h =>
                {
                    GetWindowThreadProcessId(h, out uint targetPid);
                    return targetPid == pid;
                });
            }
            else
            {
                _currentSourceHwnds.Remove(handle);
            }

            var remainingWindows = new List<IntPtr>(_currentSourceHwnds);

            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (remainingWindows.Count > 0)
                {
                    ExecuteShow(remainingWindows, _lastCardScreenX, _lastCardScreenY, _lastCardWidth, _lastCardHeight);
                }
                else
                {
                    ExecuteHide();
                }
            });
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
            _isRedrawing = true;
            try
            {
                _hideTimer.Stop();
                _peekTimer.Stop();

                if (_peekingHwnd != IntPtr.Zero)
                {
                    SafeToggleAeroPeek(false, _peekingHwnd);
                    _peekingHwnd = IntPtr.Zero;
                }
                else if (_isPeekActive)
                {
                    SafeToggleAeroPeek(false, IntPtr.Zero);
                }

                foreach (var thumb in _thumbHandles)
                {
                    Win32Helper.DwmUnregisterThumbnail(thumb);
                }
                _thumbHandles.Clear();
                _currentSourceHwnds.Clear();
                _rootStackPanel.Children.Clear();

                _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, ThumbWidth, ThumbHeight));
            }
            finally
            {
                _isRedrawing = false;
            }
        }
        #endregion
    }
}