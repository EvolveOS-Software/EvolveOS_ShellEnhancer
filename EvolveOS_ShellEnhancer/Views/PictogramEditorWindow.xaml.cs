// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using WinRT.Interop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Storage;
using System.Linq;
using EvolveOS_ShellEnhancer.Models;
using EvolveOS_ShellEnhancer.Utilities.Helpers;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class PictogramEditorWindow : Window
    {
        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private bool _isDragging = false;
        private int _dragOffsetX;
        private int _dragOffsetY;

        private AppItem _targetApp;
        private AppCategory? _targetCategory;
        private IEnumerable<AppCategory>? _allCategories;
        private Action _onSave;

        private string? _selectedImagePath;
        private string? _selectedTintColorHex;

        private ImageSource? _originalIconSource;
        private DispatcherTimer? _debounceTimer;

        public PictogramEditorWindow(AppItem targetApp, AppCategory? targetCategory, IEnumerable<AppCategory>? allCategories, Action onSave)
        {
            this.InitializeComponent();

            _targetApp = targetApp;
            _targetCategory = targetCategory;
            _allCategories = allCategories;
            _onSave = onSave;

            _hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            ConfigureWindow();
            string savedTheme = Utilities.Managers.SettingsEngine.Shell_AppTheme ?? "Default";
            SetTheme(savedTheme);

            LoadInitialData();
        }

        private void ConfigureWindow()
        {
            int style = Win32Helper.GetWindowLong(_hWnd, Win32Helper.GWL_STYLE);
            Win32Helper.SetWindowLong(_hWnd, Win32Helper.GWL_STYLE, style & ~Win32Helper.WS_CAPTION & ~Win32Helper.WS_THICKFRAME);

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            int width = 740;
            int height = 520;
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);

            if (displayArea != null)
            {
                int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
                int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;
                _appWindow.MoveAndResize(new RectInt32(centeredX, centeredY, width, height));
            }
        }

        public void SetTheme(string theme)
        {
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme == "Light" ? ElementTheme.Light : (theme == "Dark" ? ElementTheme.Dark : ElementTheme.Default);
            }
            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();
        }

        private async void LoadInitialData()
        {
            PreviewName.Text = _targetApp.Name;
            _originalIconSource = _targetApp.IconSource;

            if (_targetApp.IconSource != null)
            {
                PreviewImage.Source = _targetApp.IconSource;
                PreviewImage.Visibility = Visibility.Visible;
                PreviewIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                PreviewIcon.Glyph = _targetApp.FallbackGlyph;
                PreviewIcon.Visibility = Visibility.Visible;
                PreviewImage.Visibility = Visibility.Collapsed;
            }

            if (_targetCategory == null)
            {
                GroupScopeItem.IsEnabled = false;
            }

            TintTileRadio.IsChecked = _targetApp.IsTileTinted;
            TintIconRadio.IsChecked = _targetApp.IsIconTinted;

            // Failsafe: Enforce one selection to prevent both being unchecked initially
            if (TintTileRadio.IsChecked != true && TintIconRadio.IsChecked != true)
            {
                TintTileRadio.IsChecked = true;
            }

            if (!string.IsNullOrEmpty(_targetApp.TintColor) && _targetApp.TintColor != "NONE")
            {
                EnableTintToggle.IsOn = true;
                _selectedTintColorHex = _targetApp.TintColor.Split('_')[0];
                await UpdateLivePreviewTintAsync();
            }
        }

        #region Custom TitleBar Dragging
        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                if (sender is UIElement element) element.CapturePointer(e.Pointer);

                Win32Helper.GetWindowRect(_hWnd, out Win32Helper.RECT rect);
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
                Win32Helper.SetWindowPos(_hWnd, IntPtr.Zero, pt.X - _dragOffsetX, pt.Y - _dragOffsetY, 0, 0, 0x0015);
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

        #region Tinting Logic
        private async void EnableTintToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (CustomColorExpander != null)
                CustomColorExpander.IsExpanded = EnableTintToggle.IsOn;

            await UpdateLivePreviewTintAsync();
        }

        private async void ColorPalette_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPalette.SelectedItem is Rectangle rect)
            {
                _selectedTintColorHex = rect.Tag.ToString();
                await UpdateLivePreviewTintAsync();
            }
        }

        private void AdvancedColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            var c = args.NewColor;
            _selectedTintColorHex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

            if (_debounceTimer == null)
            {
                _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                _debounceTimer.Tick += async (s, ev) =>
                {
                    _debounceTimer.Stop();
                    await UpdateLivePreviewTintAsync();
                };
            }
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async void ApplyCustomColor_Click(object sender, RoutedEventArgs e)
        {
            var color = AdvancedColorPicker.Color;
            _selectedTintColorHex = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            await UpdateLivePreviewTintAsync();
        }

        private async void TintOption_Changed(object sender, RoutedEventArgs e)
        {
            await UpdateLivePreviewTintAsync();
        }

        private async Task UpdateLivePreviewTintAsync()
        {
            if (!EnableTintToggle.IsOn || string.IsNullOrEmpty(_selectedTintColorHex))
            {
                PreviewIcon.ClearValue(FontIcon.ForegroundProperty);
                PreviewImage.Source = string.IsNullOrEmpty(_selectedImagePath) ? _originalIconSource : new BitmapImage(new Uri(_selectedImagePath));
                PreviewTileBackground.Background = new SolidColorBrush(Colors.Transparent);
                return;
            }

            try
            {
                string hex = _selectedTintColorHex.Split('_')[0].Replace("#", "");
                byte a = 255, r = 0, g = 0, b = 0;

                if (hex.Length == 8)
                {
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }

                Color parsedColor = Color.FromArgb(a, r, g, b);
                var solidBrush = new SolidColorBrush(parsedColor);

                if (PreviewIcon.Visibility == Visibility.Visible)
                {
                    PreviewIcon.Foreground = (TintIconRadio.IsChecked == true) ? solidBrush : null;
                }

                if (PreviewImage.Visibility == Visibility.Visible)
                {
                    if (TintIconRadio.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(_selectedImagePath))
                        {
                            var file = await StorageFile.GetFileFromPathAsync(_selectedImagePath);
                            using var stream = await file.OpenReadAsync();
                            PreviewImage.Source = await IconTintHelper.GetTintedImageAsync(stream, parsedColor);
                        }
                        else
                        {
                            using var stream = await StartMenuHelper.ExtractAppIconStreamAsync(_targetApp);
                            if (stream != null)
                            {
                                stream.Seek(0);
                                PreviewImage.Source = await IconTintHelper.GetTintedImageAsync(stream, parsedColor);
                            }
                        }
                    }
                    else
                    {
                        PreviewImage.Source = !string.IsNullOrEmpty(_selectedImagePath) ? new BitmapImage(new Uri(_selectedImagePath)) : _originalIconSource;
                    }
                }

                PreviewTileBackground.Background = (TintTileRadio.IsChecked == true) ? solidBrush : new SolidColorBrush(Colors.Transparent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to apply tint: {ex.Message}");
            }
        }

        private void CustomColorExpander_SizeChanged(object sender, SizeChangedEventArgs e)
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

        #region General Logic
        private async void BtnBrowseImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? filePath = Win32FileDialogHelper.ShowOpenFilePicker(
                    this, "Select Custom Icon Image", "Image Files", "*.png;*.jpg;*.jpeg;*.svg;*.ico");

                if (!string.IsNullOrEmpty(filePath))
                {
                    _selectedImagePath = filePath;
                    PreviewImage.Source = new BitmapImage(new Uri(filePath));
                    PreviewImage.Visibility = Visibility.Visible;
                    PreviewIcon.Visibility = Visibility.Collapsed;

                    await UpdateLivePreviewTintAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to pick image: {ex.Message}");
            }
        }

        private async void BtnResetIcon_Click(object sender, RoutedEventArgs e)
        {
            _selectedImagePath = null;
            EnableTintToggle.IsOn = false;
            _selectedTintColorHex = null;

            TintTileRadio.IsChecked = true;
            TintIconRadio.IsChecked = false;

            if (!string.IsNullOrEmpty(_targetApp.ExecutablePath) &&
                _targetApp.ExecutablePath != "PINNED_FOLDER" &&
                _targetApp.ExecutablePath != "TAB_DATA")
            {
                try
                {
                    var newSource = await StartMenuHelper.ExtractAppIconAsync(_targetApp);
                    if (newSource != null)
                    {
                        _originalIconSource = newSource;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to extract original icon: {ex.Message}");
                }
            }

            if (_originalIconSource != null)
            {
                PreviewImage.Source = _originalIconSource;
                PreviewImage.Visibility = Visibility.Visible;
                PreviewIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                PreviewImage.Source = null;
                PreviewIcon.Glyph = _targetApp.FallbackGlyph;
                PreviewIcon.Visibility = Visibility.Visible;
                PreviewImage.Visibility = Visibility.Collapsed;
            }

            await UpdateLivePreviewTintAsync();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) btn.IsEnabled = false;

            string scope = (ScopeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Single";
            var baseApps = new List<AppItem>();

            if (scope == "Single") baseApps.Add(_targetApp);
            else if (scope == "Group" && _targetCategory != null) baseApps.AddRange(_targetCategory.Apps);
            else if (scope == "All" && _allCategories != null)
            {
                foreach (var cat in _allCategories) baseApps.AddRange(cat.Apps);
            }

            var appsToProcess = new List<AppItem>();
            foreach (var app in baseApps)
            {
                appsToProcess.Add(app);
                if (app.FolderApps != null) appsToProcess.AddRange(app.FolderApps);
            }
            appsToProcess = appsToProcess.Distinct().ToList();

            foreach (var app in appsToProcess)
            {
                if (!string.IsNullOrEmpty(_selectedImagePath))
                {
                    app.CustomImagePath = _selectedImagePath;
                    app.IconSource = new BitmapImage(new Uri(_selectedImagePath));
                }
                else if (_selectedImagePath == null && _targetApp == app)
                {
                    app.CustomImagePath = null;
                }

                if (EnableTintToggle.IsOn && !string.IsNullOrEmpty(_selectedTintColorHex))
                {
                    string baseHex = _selectedTintColorHex.Split('_')[0];
                    string tFlag = (TintTileRadio.IsChecked == true) ? "1" : "0";
                    string iFlag = (TintIconRadio.IsChecked == true) ? "1" : "0";

                    app.TintColor = $"{baseHex}_{tFlag}_{iFlag}";
                }
                else if (!EnableTintToggle.IsOn)
                {
                    app.TintColor = null;
                }

                await StartMenuHelper.ApplyTintAsync(app);
            }

            _onSave?.Invoke();
            this.Close();
        }
        #endregion
    }
}