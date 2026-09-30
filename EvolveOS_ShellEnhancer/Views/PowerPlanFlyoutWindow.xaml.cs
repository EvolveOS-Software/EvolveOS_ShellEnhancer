// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class PowerPlanFlyoutWindow : Window
    {
        private readonly IntPtr _hWnd;
        private readonly AppWindow _appWindow;
        private bool _isClosing = false;

        public Action? OnClosed { get; set; }

        #region Animation Engine Fields
        private enum AnimState { None, Entrance, Exit }
        private AnimState _currentAnimState = AnimState.None;
        private bool _isAnimating = false;
        private DateTime _animStartTime;
        private int _animDuration;
        private int _startX, _startY;
        private int _targetX, _targetY, _targetW, _targetH;
        #endregion

        private const int DWMWA_EXCLUDED_FROM_PEEK = 12;
        private const int HWND_TOPMOST = -1;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLongPtr(IntPtr hWnd, int nIndex);

        public PowerPlanFlyoutWindow()
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

            RemoveWindowBorders(_hWnd);
            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

            int exclude = 1;
            DwmSetWindowAttribute(_hWnd, DWMWA_EXCLUDED_FROM_PEEK, ref exclude, sizeof(int));

            this.Activated += PowerPlanFlyoutWindow_Activated;

            string savedTheme = SettingsEngine.Shell_AppTheme ?? "Default";
            SetTheme(savedTheme);

            int exStyle = GetWindowLongPtr(_hWnd, GWL_EXSTYLE);

            exStyle &= ~WS_EX_APPWINDOW;
            exStyle |= WS_EX_TOOLWINDOW;

            SetWindowLongPtr(_hWnd, GWL_EXSTYLE, exStyle);

            _appWindow.MoveAndResize(new RectInt32(-32000, -32000, 260, 200));
            _appWindow.Show();
        }

        private void SetTheme(string theme)
        {
            if (theme == "Light") RootPanel.RequestedTheme = ElementTheme.Light;
            else if (theme == "Dark") RootPanel.RequestedTheme = ElementTheme.Dark;
            else RootPanel.RequestedTheme = ElementTheme.Default;
        }

        private void PowerPlanFlyoutWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && !_isClosing)
            {
                HideMenu();
            }
        }

        public bool IsCurrentlyVisible()
        {
            return _appWindow.Position.Y > -32000;
        }

        private string GetTaskbarPosition(int x, int y)
        {
            var displayArea = DisplayArea.GetFromPoint(
                new PointInt32(x, y),
                DisplayAreaFallback.Primary);

            var workArea = displayArea.WorkArea;
            var bounds = displayArea.OuterBounds;

            if (workArea.Y > bounds.Y) return "Top";
            if (workArea.X > bounds.X) return "Left";
            if (workArea.Width < bounds.Width) return "Right";

            return "Bottom";
        }

        public void ShowMenu(int anchorX, int anchorY, string _ignoredTaskbarPosition)
        {
            _isClosing = false;
            var plans = PowerPlanManager.GetPowerPlans();
            PlansList.ItemsSource = plans;

            int width = 280;
            int height = 70 + (plans.Count * 44);
            int margin = 12;

            int finalX = anchorX;
            int finalY = anchorY;

            string actualTaskbarPos = GetTaskbarPosition(anchorX, anchorY);

            var displayArea = DisplayArea.GetFromPoint(new PointInt32(anchorX, anchorY), DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            switch (actualTaskbarPos)
            {
                case "Top":
                    finalX = anchorX - (width / 2);
                    finalY = anchorY + margin;
                    _startX = finalX; _startY = finalY - height - 20;
                    break;
                case "Left":
                    finalX = anchorX + margin;
                    finalY = anchorY - (height / 2);

                    if (finalY + height > workArea.Y + workArea.Height - margin)
                        finalY = workArea.Y + workArea.Height - height - margin;

                    _startX = finalX - width - 20; _startY = finalY;
                    break;
                case "Right":
                    finalX = anchorX - width - margin;
                    finalY = anchorY - (height / 2);

                    if (finalY + height > workArea.Y + workArea.Height - margin)
                        finalY = workArea.Y + workArea.Height - height - margin;

                    _startX = finalX + width + 20; _startY = finalY;
                    break;
                case "Bottom":
                default:
                    finalX = anchorX - (width / 2);

                    double scale = this.Content?.XamlRoot?.RasterizationScale ?? 1.0;
                    int offsetPhysical = (int)(25 * scale);

                    finalY = anchorY - height - margin - offsetPhysical;
                    _startX = finalX; _startY = finalY + height + 20;
                    break;
            }

            if (finalX < workArea.X + margin)
                finalX = workArea.X + margin;
            if (finalX + width > workArea.X + workArea.Width - margin)
                finalX = workArea.X + workArea.Width - width - margin;

            _targetX = finalX;
            _targetY = finalY;
            _targetW = width;
            _targetH = height;

            if (LivePreviewWindow.EnableAnimations)
            {
                _currentAnimState = AnimState.Entrance;
                _animDuration = (int)(200 / Math.Max(0.1, LivePreviewWindow.AnimationSpeed));
                RootPanel.Opacity = 0;
                StartBoundsAnimation();
            }
            else
            {
                _appWindow.MoveAndResize(new RectInt32(_targetX, _targetY, _targetW, _targetH));
                RootPanel.Opacity = 1;
            }

            SetWindowPos(_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            Win32Helper.SetForegroundWindow(_hWnd);
        }

        public void HideMenu()
        {
            if (_isClosing) return;
            _isClosing = true;

            OnClosed?.Invoke();

            if (LivePreviewWindow.EnableAnimations)
            {
                _currentAnimState = AnimState.Exit;
                _animDuration = (int)(150 / Math.Max(0.1, LivePreviewWindow.AnimationSpeed));

                _startX = _appWindow.Position.X;
                _startY = _appWindow.Position.Y;

                string actualTaskbarPos = GetTaskbarPosition(_appWindow.Position.X, _appWindow.Position.Y);

                if (actualTaskbarPos == "Top") _targetY = _startY - _appWindow.Size.Height - 20;
                else if (actualTaskbarPos == "Left") _targetX = _startX - _appWindow.Size.Width - 20;
                else if (actualTaskbarPos == "Right") _targetX = _startX + _appWindow.Size.Width + 20;
                else _targetY = _startY + _appWindow.Size.Height + 20;

                StartBoundsAnimation();
            }
            else
            {
                _appWindow.MoveAndResize(new RectInt32(-32000, -32000, 260, 200));
                RootPanel.Opacity = 0;
            }
        }

        private void PlansList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlansList.SelectedItem is PowerPlan selectedPlan && !selectedPlan.IsActive)
            {
                PowerPlanManager.SetActivePlan(selectedPlan.Id);
                HideMenu();
            }
        }

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

        private double CalculateEasing(double t)
        {
            string style = LivePreviewWindow.AnimationStyle ?? "Standard";
            if (t <= 0) return 0;
            if (t >= 1) return 1;

            switch (style)
            {
                case "Glide": return 1 - Math.Pow(1 - t, 3);
                case "Spring": return 1 - Math.Exp(-t * 6) * Math.Cos(t * Math.PI * 1.5);
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

            double easeBounds = CalculateEasing(t);
            double easeOpacity = 1 - Math.Pow(2, -10 * t);

            if (t >= 1.0) { easeBounds = 1.0; easeOpacity = 1.0; }

            int curX = (int)(_startX + (_targetX - _startX) * easeBounds);
            int curY = (int)(_startY + (_targetY - _startY) * easeBounds);

            _appWindow.MoveAndResize(new RectInt32(curX, curY, _targetW, _targetH));

            if (_currentAnimState == AnimState.Entrance)
            {
                RootPanel.Opacity = Math.Min(1.0, easeOpacity * 1.5);
            }
            else if (_currentAnimState == AnimState.Exit)
            {
                RootPanel.Opacity = 1 - easeOpacity;
            }

            if (t >= 1.0)
            {
                StopBoundsAnimation();
                if (_currentAnimState == AnimState.Exit)
                {
                    _appWindow.MoveAndResize(new RectInt32(-32000, -32000, 260, 200));
                }
                _currentAnimState = AnimState.None;
            }
        }
        #endregion
    }
}