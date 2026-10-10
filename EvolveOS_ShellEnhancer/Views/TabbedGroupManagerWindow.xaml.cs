// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using System.Collections.ObjectModel;
using Windows.Graphics;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class TabbedGroupManagerWindow : Window
    {
        private readonly AppCategory _targetGroup;
        private readonly Action _onSaveCompleted;
        public ObservableCollection<AppCategory> EditingTabs { get; set; } = new();

        private EvolveAcrylicController _acrylicController;

        private static bool IsSystemInDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val) return val == 0;
            }
            catch { }
            return true;
        }

        private bool _isDragging = false;
        private int _dragOffsetX;
        private int _dragOffsetY;

        public TabbedGroupManagerWindow(AppCategory targetGroup, Action onSaveCompleted)
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            _acrylicController = new EvolveAcrylicController(hwnd);

            _targetGroup = targetGroup;
            _onSaveCompleted = onSaveCompleted;

            ConfigureWindow();

            string savedTheme = SettingsEngine.Shell_AppTheme ?? "Default";
            bool isLight = savedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase) ||
                           (savedTheme.Equals("Default", StringComparison.OrdinalIgnoreCase) && !IsSystemInDarkMode());

            _acrylicController.Initialize(isLight);
            SetTheme(savedTheme);

            foreach (var tab in _targetGroup.Tabs)
            {
                var clone = new AppCategory { Name = tab.Name, TabColor = tab.TabColor };
                foreach (var app in tab.Apps) clone.Apps.Add(app);
                EditingTabs.Add(clone);
            }

            TabsList.ItemsSource = EditingTabs;
            if (EditingTabs.Count > 0) TabsList.SelectedIndex = 0;
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

            int width = 740;
            int height = 520;
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

            if (displayArea != null)
            {
                int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
                int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;
                appWindow.MoveAndResize(new RectInt32(centeredX, centeredY, width, height));
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

        #region Dragging Logic
        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                if (sender is UIElement element) element.CapturePointer(e.Pointer);

                var hwnd = WindowNative.GetWindowHandle(this);
                Win32Helper.GetWindowRect(hwnd, out Win32Helper.RECT rect);
                Win32Helper.GetCursorPos(out Win32Helper.POINT pt);

                _dragOffsetX = pt.X - rect.Left;
                _dragOffsetY = pt.Y - rect.Top;
                e.Handled = true;
            }
        }

        private void TitleBar_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                Win32Helper.GetCursorPos(out Win32Helper.POINT pt);
                Win32Helper.SetWindowPos(WindowNative.GetWindowHandle(this), IntPtr.Zero, pt.X - _dragOffsetX, pt.Y - _dragOffsetY, 0, 0, 0x0015);
            }
        }

        private void TitleBar_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                if (sender is UIElement element) element.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }
        #endregion

        #region Editor Logic
        private void TabsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedTab = TabsList.SelectedItem as AppCategory;
            bool hasSelection = selectedTab != null;

            TabNameBox.IsEnabled = hasSelection;
            ColorPalette.IsEnabled = hasSelection;

            if (hasSelection)
            {
                TabNameBox.Text = selectedTab!.Name ?? "";
            }
            else
            {
                TabNameBox.Text = "";
            }
        }

        private void TabNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TabsList.SelectedItem is AppCategory selectedTab)
            {
                selectedTab.Name = TabNameBox.Text;
            }
        }

        private void AddTab_Click(object sender, RoutedEventArgs e)
        {
            var newTab = new AppCategory { Name = $"Tab {EditingTabs.Count + 1}", TabColor = "NONE" };
            EditingTabs.Add(newTab);
            TabsList.SelectedItem = newTab;
        }

        private void RemoveTab_Click(object sender, RoutedEventArgs e)
        {
            if (TabsList.SelectedItem is AppCategory selectedTab)
            {
                EditingTabs.Remove(selectedTab);
            }
        }

        private void ColorPalette_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabsList.SelectedItem is AppCategory selectedTab && ColorPalette.SelectedItem is Rectangle rect)
            {
                string hexColor = rect.Tag.ToString()!;
                selectedTab.TabColor = hexColor;
            }
        }

        private void AdvancedColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            // Optional: Live preview of custom color picker
        }

        private void ApplyCustomColor_Click(object sender, RoutedEventArgs e)
        {
            if (TabsList.SelectedItem is AppCategory selectedTab)
            {
                var color = AdvancedColorPicker.Color;
                string hex = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                selectedTab.TabColor = hex;
            }
        }

        private void CustomColorExpander_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            if (CustomColorExpander.IsExpanded)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(50);
                    RightScrollViewer.ChangeView(null, RightScrollViewer.ScrollableHeight, null, false);
                });
            }
        }
        #endregion

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            _targetGroup.Tabs.Clear();
            foreach (var tab in EditingTabs)
            {
                _targetGroup.Tabs.Add(tab);
            }

            _onSaveCompleted?.Invoke();
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}