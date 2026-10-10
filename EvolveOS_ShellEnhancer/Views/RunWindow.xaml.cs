// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class RunWindow : Window
    {
        private EvolveAcrylicController _acrylicController;

        private static bool IsSystemInDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val) return val == 0;
            }
            catch { }
            return true;
        }

        public RunWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            _acrylicController = new EvolveAcrylicController(hwnd);

            ConfigureWindow();

            string savedTheme = SettingsEngine.Shell_AppTheme ?? "Default";
            bool isLight = savedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase) ||
                           (savedTheme.Equals("Default", StringComparison.OrdinalIgnoreCase) && !IsSystemInDarkMode());

            _acrylicController.Initialize(isLight);
            SetTheme(savedTheme);

            if (this.Content is FrameworkElement rootElement && rootElement is Panel rootPanel)
            {
                rootPanel.Background = new SolidColorBrush(Colors.Transparent);
            }

            this.Activated += (s, e) => InputTextBox.Focus(FocusState.Programmatic);
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
            }

            int width = 420;
            int height = 220;
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

            if (displayArea != null)
            {
                int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
                int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(centeredX, centeredY, width, height));
            }
        }

        public void SetTheme(string theme)
        {
            bool isLight = theme.Equals("Light", StringComparison.OrdinalIgnoreCase) ||
                           (theme.Equals("Default", StringComparison.OrdinalIgnoreCase) && !IsSystemInDarkMode());

            string acrylicStyle = SettingsEngine.Shell_AcrylicStyle ?? "Acrylic";
            bool isSolidMode = acrylicStyle.Equals("Solid", StringComparison.OrdinalIgnoreCase) || acrylicStyle.Equals("None", StringComparison.OrdinalIgnoreCase);

            if (this.Content is Panel root)
            {
                root.RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;

                if (isSolidMode)
                {
                    _acrylicController?.ClearAcrylic();
                    root.Background = isLight ?
                        new SolidColorBrush(Colors.WhiteSmoke) :
                        new SolidColorBrush(ColorHelper.FromArgb(255, 32, 32, 32));

                    this.SystemBackdrop = null;
                }
                else
                {
                    root.Background = new SolidColorBrush(Colors.Transparent);

                    if (this.SystemBackdrop is not AlwaysActiveAcrylicBackdrop)
                    {
                        this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();
                    }

                    if (this.SystemBackdrop is AlwaysActiveAcrylicBackdrop backdrop)
                    {
                        backdrop.UpdateLive();
                    }

                    double opacity = SettingsEngine.Shell_AcrylicOpacity;
                    double luminosity = SettingsEngine.Shell_AcrylicLuminosity;

                    _acrylicController?.UpdateStyle(acrylicStyle, opacity, luminosity, isLight);
                }
            }
        }

        #region Manual Window Dragging
        private bool _isDragging = false;
        private int _dragOffsetX;
        private int _dragOffsetY;

        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;

                if (sender is UIElement element)
                {
                    element.CapturePointer(e.Pointer);
                }

                var hwnd = WindowNative.GetWindowHandle(this);
                GetWindowRect(hwnd, out RECT rect);
                GetCursorPos(out POINT pt);

                _dragOffsetX = pt.X - rect.Left;
                _dragOffsetY = pt.Y - rect.Top;

                e.Handled = true;
            }
        }

        private void TitleBar_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                GetCursorPos(out POINT pt);
                var hwnd = WindowNative.GetWindowHandle(this);

                SetWindowPos(hwnd, IntPtr.Zero, pt.X - _dragOffsetX, pt.Y - _dragOffsetY, 0, 0, 0x0015);
            }
        }

        private void TitleBar_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;

                if (sender is UIElement element)
                {
                    element.ReleasePointerCapture(e.Pointer);
                }
                e.Handled = true;
            }
        }

        #endregion

        private void BtnOk_Click(object sender, RoutedEventArgs e) => ExecuteCommand();

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => this.Close();

        private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                ExecuteCommand();
                e.Handled = true;
            }
        }

        private void ExecuteCommand()
        {
            string command = InputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(command)) return;

            try
            {
                // Clean, centralized, and flicker-free!
                CommandExecutor.ExecuteRunDialogCommand(command);

                this.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to run command: {ex.Message}");
                // Fallback: Optionally spawn MessageWindow here to show an error
                // new MessageWindow(MessageWindowState.Warning).Activate();
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            string? filePath = Win32FileDialogHelper.ShowOpenFilePicker(
                window: this,
                title: "Browse",
                filterName: "Programs",
                filterPattern: "*.exe;*.pif;*.com;*.bat;*.cmd"
            );

            if (!string.IsNullOrEmpty(filePath))
            {
                InputTextBox.Text = $"\"{filePath}\"";
                InputTextBox.SelectAll();
                InputTextBox.Focus(FocusState.Programmatic);
            }
        }
    }
}