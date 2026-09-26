// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;
using Windows.Storage.Pickers;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class RunWindow : Window
    {
        public RunWindow()
        {
            this.InitializeComponent();

            ConfigureWindow();

            string savedTheme = SettingsEngine.Shell_AppTheme ?? "Default";
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
            if (this.Content is FrameworkElement root)
            {
                if (theme == "Light")
                    root.RequestedTheme = ElementTheme.Light;
                else if (theme == "Dark")
                    root.RequestedTheme = ElementTheme.Dark;
                else
                    root.RequestedTheme = ElementTheme.Default;
            }

            this.SystemBackdrop = null;
            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();
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