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
        public static bool EnableAnimations { get; set; } = true;
        public static string AnimationStyle { get; set; } = "Standard";
        public static double AnimationSpeed { get; set; } = 1.0;

        private const int ThumbWidth = 220;
        private const int ThumbHeight = 142;
        private const int ActionPanelHeight = 40;
        private const int SlotMargin = 10;
        private const int HighlightPaddingX = 5;

        private readonly SolidColorBrush _hoverBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255));
        private readonly SolidColorBrush _transparentBrush = new SolidColorBrush(Colors.Transparent);
        private readonly SolidColorBrush _hitTestBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0));

        private DispatcherTimer _peekTimer;
        private IntPtr _peekingHwnd = IntPtr.Zero;

        private bool _isPeekActive = false;
        private bool _isRedrawing = false;

        private enum AnimState { None, Entrance, Redraw, Exit }
        private AnimState _currentAnimState = AnimState.None;
        private bool _isAnimating = false;
        private DateTime _animStartTime;
        private int _animDuration;
        private int _startX, _startY, _startW, _startH;
        private int _targetX, _targetY, _targetW, _targetH;
        private byte _lastAlpha = 255;

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
                Background = _hitTestBrush,
                Padding = new Thickness(0),
                Spacing = 0
            };

            _rootStackPanel.PointerEntered += (s, e) => { _hideTimer?.Stop(); };
            _rootStackPanel.PointerExited += (s, e) => { if (!_isRedrawing) StartHideTimer(); };

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

        #region VSync Native Animation Engine
        private void StartBoundsAnimation()
        {
            _animStartTime = DateTime.Now;
            _isAnimating = true;
            CompositionTarget.Rendering -= BoundsAnim_Rendering;
            CompositionTarget.Rendering += BoundsAnim_Rendering;
        }

        private void StopBoundsAnimation()
        {
            _isAnimating = false;
            CompositionTarget.Rendering -= BoundsAnim_Rendering;
        }

        private double CalculateEasing(double t, string style)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;

            switch (style)
            {
                case "Glide": return 1 - Math.Pow(1 - t, 3);
                case "Spring": return 1 - Math.Exp(-t * 6) * Math.Cos(t * Math.PI * 1.5);
                case "Bounce":
                    double n1 = 7.5625;
                    double d1 = 2.75;
                    if (t < 1 / d1) return n1 * t * t;
                    else if (t < 2 / d1) return n1 * (t -= 1.5 / d1) * t + 0.75;
                    else if (t < 2.5 / d1) return n1 * (t -= 2.25 / d1) * t + 0.9375;
                    else return n1 * (t -= 2.625 / d1) * t + 0.984375;
                case "Elastic":
                    double c4 = (2 * Math.PI) / 0.3;
                    return Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1;
                case "Fade":
                case "Standard":
                default: return 1 - Math.Pow(2, -10 * t);
            }
        }

        private void BoundsAnim_Rendering(object? sender, object e)
        {
            if (!_isAnimating) return;

            double elapsed = (DateTime.Now - _animStartTime).TotalMilliseconds;
            double t = elapsed / _animDuration;
            if (t >= 1.0) t = 1.0;

            double easeBounds = CalculateEasing(t, AnimationStyle);
            double easeOpacity = CalculateEasing(t, "Standard");

            if (t >= 1.0)
            {
                easeBounds = 1.0;
                easeOpacity = 1.0;
            }

            int curX = (int)(_startX + (_targetX - _startX) * easeBounds);
            int curY = (int)(_startY + (_targetY - _startY) * easeBounds);
            int curW = (int)(_startW + (_targetW - _startW) * easeBounds);
            int curH = (int)(_startH + (_targetH - _startH) * easeBounds);

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(curX, curY, curW, curH));

            if (_currentAnimState == AnimState.Entrance)
            {
                double opacity = Math.Min(1.0, easeOpacity * 1.5);
                _rootStackPanel.Opacity = opacity;
                byte alpha = (byte)(opacity * 255);

                if (alpha != _lastAlpha)
                {
                    _lastAlpha = alpha;
                    foreach (var thumb in _thumbHandles)
                    {
                        var props = new Win32Helper.DWM_THUMBNAIL_PROPERTIES { dwFlags = Win32Helper.DWM_TNP_OPACITY, opacity = alpha };
                        Win32Helper.DwmUpdateThumbnailProperties(thumb, ref props);
                    }
                }
            }
            else if (_currentAnimState == AnimState.Exit)
            {
                double fadeOutEase = 1 - easeOpacity;
                _rootStackPanel.Opacity = fadeOutEase;
                byte alpha = (byte)(fadeOutEase * 255);

                if (alpha != _lastAlpha)
                {
                    _lastAlpha = alpha;
                    foreach (var thumb in _thumbHandles)
                    {
                        var props = new Win32Helper.DWM_THUMBNAIL_PROPERTIES { dwFlags = Win32Helper.DWM_TNP_OPACITY, opacity = alpha };
                        Win32Helper.DwmUpdateThumbnailProperties(thumb, ref props);
                    }
                }
            }
            else if (_currentAnimState == AnimState.Redraw)
            {
                _rootStackPanel.Opacity = 1.0;
                if (_lastAlpha != 255)
                {
                    _lastAlpha = 255;
                    foreach (var thumb in _thumbHandles)
                    {
                        var props = new Win32Helper.DWM_THUMBNAIL_PROPERTIES { dwFlags = Win32Helper.DWM_TNP_OPACITY, opacity = 255 };
                        Win32Helper.DwmUpdateThumbnailProperties(thumb, ref props);
                    }
                }
            }

            if (t >= 1.0)
            {
                StopBoundsAnimation();

                if (_currentAnimState == AnimState.Exit)
                {
                    foreach (var thumb in _thumbHandles) Win32Helper.DwmUnregisterThumbnail(thumb);
                    _thumbHandles.Clear();
                    _currentSourceHwnds.Clear();
                    _rootStackPanel.Children.Clear();
                    _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, ThumbWidth, ThumbHeight));
                }

                _isRedrawing = false;
                _currentAnimState = AnimState.None;
            }
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
            bool isSame = false;
            if (sourceHwnds != null && _currentSourceHwnds.Count == sourceHwnds.Count)
            {
                isSame = true;
                for (int i = 0; i < sourceHwnds.Count; i++)
                {
                    if (_currentSourceHwnds[i] != sourceHwnds[i]) { isSame = false; break; }
                }
            }

            if (isSame && _lastCardScreenX == cardScreenX && _lastCardScreenY == cardScreenY && _appWindow.Position.Y > -32000)
            {
                _hideTimer.Stop();
                if (_currentAnimState != AnimState.Exit) return;
            }

            _isRedrawing = true;
            StopBoundsAnimation();
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

            if (sourceHwnds == null || sourceHwnds.Count == 0)
            {
                _isRedrawing = false;
                return;
            }

            _currentSourceHwnds.AddRange(sourceHwnds);

            var point = new Windows.Graphics.PointInt32(cardScreenX, cardScreenY);
            var displayArea = DisplayArea.GetFromPoint(point, DisplayAreaFallback.Nearest);

            string position = TaskbarManager.GetPositionForDisplay(displayArea.DisplayId.Value.ToString());
            bool isVertical = (position == "Left" || position == "Right");

            _rootStackPanel.Orientation = isVertical ? Orientation.Vertical : Orientation.Horizontal;

            int itemWidth = ThumbWidth + (HighlightPaddingX * 2) + (SlotMargin * 2);
            int itemHeight = ThumbHeight + ActionPanelHeight + (SlotMargin * 2);

            int totalWidth, totalHeight;
            if (isVertical)
            {
                totalWidth = itemWidth;
                totalHeight = sourceHwnds.Count * itemHeight;
            }
            else
            {
                totalWidth = sourceHwnds.Count * itemWidth;
                totalHeight = itemHeight;
            }

            int x, y;

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

            bool isFirstShow = _appWindow.Position.Y <= -32000;
            int startX = x, startY = y, startW = totalWidth, startH = totalHeight;

            if (isFirstShow)
            {
                if (EnableAnimations && AnimationStyle != "Fade")
                {
                    if (position == "Top") startY = y - totalHeight - 15;
                    else if (position == "Left") { startY = y; startX = x - totalWidth - 15; }
                    else if (position == "Right") { startY = y; startX = x + totalWidth + 15; }
                    else startY = y + totalHeight + 15;
                }

                _rootStackPanel.Opacity = 0;
            }
            else
            {
                startX = _appWindow.Position.X;
                startY = _appWindow.Position.Y;
                startW = _appWindow.Size.Width;
                startH = _appWindow.Size.Height;
                _rootStackPanel.Opacity = 1.0;
            }

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(startX, startY, startW, startH));
            SetWindowPos(_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            TaskbarManager.EnsureAllTaskbarsTopmost();

            IntPtr currentHwnd = WindowNative.GetWindowHandle(this);
            byte initialOpacity = (byte)(isFirstShow && EnableAnimations ? 0 : 255);

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
                    Margin = new Thickness(HighlightPaddingX + SlotMargin, SlotMargin + 3, SlotMargin + 12, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 1
                };

                StackPanel actionPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, SlotMargin + 8),
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
                    _hideTimer.Stop();
                    if (_isRedrawing) return;

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
                        topOffset = (i * itemHeight) + SlotMargin + 24;
                    }
                    else
                    {
                        leftOffset = (i * itemWidth) + SlotMargin + HighlightPaddingX;
                        topOffset = SlotMargin + 24;
                    }

                    Win32Helper.DWM_THUMBNAIL_PROPERTIES props = new Win32Helper.DWM_THUMBNAIL_PROPERTIES
                    {
                        dwFlags = Win32Helper.DWM_TNP_VISIBLE | Win32Helper.DWM_TNP_RECTDESTINATION | Win32Helper.DWM_TNP_OPACITY,
                        fVisible = true,
                        opacity = initialOpacity,
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

            if (EnableAnimations)
            {
                _currentAnimState = isFirstShow ? AnimState.Entrance : AnimState.Redraw;
                _animDuration = (int)((isFirstShow ? 200 : 150) / Math.Max(0.1, AnimationSpeed));
                _startX = startX;
                _startY = startY;
                _startW = startW;
                _startH = startH;
                _targetX = x;
                _targetY = y;
                _targetW = totalWidth;
                _targetH = totalHeight;

                _lastAlpha = initialOpacity;
                StartBoundsAnimation();
            }
            else
            {
                _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, totalWidth, totalHeight));
                _rootStackPanel.Opacity = 1.0;
                foreach (var thumb in _thumbHandles)
                {
                    var props = new Win32Helper.DWM_THUMBNAIL_PROPERTIES { dwFlags = Win32Helper.DWM_TNP_OPACITY, opacity = 255 };
                    Win32Helper.DwmUpdateThumbnailProperties(thumb, ref props);
                }
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

            var remainingWindows = new List<IntPtr>(_currentSourceHwnds);
            remainingWindows.Remove(handle);

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

            var remainingWindows = new List<IntPtr>(_currentSourceHwnds);

            if (pid > 0)
            {
                try
                {
                    Process p = Process.GetProcessById((int)pid);
                    p.Kill();
                }
                catch { }

                remainingWindows.RemoveAll(h =>
                {
                    GetWindowThreadProcessId(h, out uint targetPid);
                    return targetPid == pid;
                });
            }
            else
            {
                remainingWindows.Remove(handle);
            }

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
            StopBoundsAnimation();
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

            if (_appWindow.Position.Y <= -32000)
            {
                _isRedrawing = false;
                return;
            }

            if (EnableAnimations)
            {
                _currentAnimState = AnimState.Exit;
                _animDuration = (int)(150 / Math.Max(0.1, AnimationSpeed));

                _startX = _appWindow.Position.X;
                _startY = _appWindow.Position.Y;
                _startW = _appWindow.Size.Width;
                _startH = _appWindow.Size.Height;

                int targetX = _startX;
                int targetY = _startY;

                if (AnimationStyle != "Fade")
                {
                    var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
                    string position = TaskbarManager.GetPositionForDisplay(displayArea.DisplayId.Value.ToString());

                    if (position == "Top") targetY = _startY - _startH - 15;
                    else if (position == "Left") targetX = _startX - _startW - 15;
                    else if (position == "Right") targetX = _startX + _startW + 15;
                    else targetY = _startY + _startH + 15;
                }

                _targetX = targetX;
                _targetY = targetY;
                _targetW = _startW;
                _targetH = _startH;

                _lastAlpha = 255;
                StartBoundsAnimation();
            }
            else
            {
                foreach (var thumb in _thumbHandles)
                {
                    Win32Helper.DwmUnregisterThumbnail(thumb);
                }
                _thumbHandles.Clear();
                _currentSourceHwnds.Clear();
                _rootStackPanel.Children.Clear();
                _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, ThumbWidth, ThumbHeight));
                _isRedrawing = false;
            }
        }
        #endregion
    }
}