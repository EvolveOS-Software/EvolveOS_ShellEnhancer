// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class DesktopTabWidgetWindow : Window, INotifyPropertyChanged
    {
        #region Fields & Properties
        public AppCategory ParentCategory { get; }
        private readonly Action _saveCallback;

        public ObservableCollection<AppItem>? SelectedTabApps =>
            ParentCategory.Tabs.Count > ParentCategory.SelectedTabIndex
                ? ParentCategory.Tabs[ParentCategory.SelectedTabIndex].Apps
                : null;

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly AppItem _placeholderItem = new AppItem { Name = "", FallbackGlyph = "" };
        private AppItem? _activeFolderApp;

        // Paging Fields
        public ObservableCollection<WidgetPageDot> PageDots { get; } = new();
        private string _currentSize = "Medium";
        private int _appsPerPage = 5;
        private int _currentPage = 0;
        private ObservableCollection<AppItem>? _currentObservedList;
        #endregion

        #region Initialization
        public DesktopTabWidgetWindow(AppCategory targetGroup, Action saveCallback)
        {
            ParentCategory = targetGroup;
            _saveCallback = saveCallback;

            this.InitializeComponent();
            SetTheme(SettingsEngine.Shell_AppTheme ?? "Default");

            var openWidgets = new List<string>(SettingsEngine.Desktop_OpenWidgets.Split('|', StringSplitOptions.RemoveEmptyEntries));
            if (!openWidgets.Contains(ParentCategory.Name))
            {
                openWidgets.Add(ParentCategory.Name);
                SettingsEngine.Desktop_OpenWidgets = string.Join("|", openWidgets);
            }

            this.Closed += (s, e) =>
            {
                var currentWidgets = new List<string>(SettingsEngine.Desktop_OpenWidgets.Split('|', StringSplitOptions.RemoveEmptyEntries));
                if (currentWidgets.Contains(ParentCategory.Name))
                {
                    currentWidgets.Remove(ParentCategory.Name);
                    SettingsEngine.Desktop_OpenWidgets = string.Join("|", currentWidgets);
                }
            };

            if (this.Content is FrameworkElement rootElement && rootElement is Panel rootPanel)
            {
                rootPanel.Background = new SolidColorBrush(Colors.Transparent);
            }

            ParentCategory.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppCategory.SelectedTabIndex))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTabApps)));
                    _currentPage = 0;
                    AttachCollectionListener();
                    ApplyWidgetSize();
                }
            };

            ConfigureWindow();
            AttachCollectionListener();
            ApplyWidgetSize();

            MainAppGrid.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(MainAppGrid_PointerWheelChanged), true);
        }

        private void AttachCollectionListener()
        {
            if (_currentObservedList != null)
            {
                _currentObservedList.CollectionChanged -= AppCollection_Changed;
            }

            _currentObservedList = SelectedTabApps;

            if (_currentObservedList != null)
            {
                _currentObservedList.CollectionChanged += AppCollection_Changed;
            }
        }

        private void AppCollection_Changed(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ApplyWidgetSize();
        }

        private void ConfigureWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_CAPTION & ~WS_THICKFRAME);

            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsAlwaysOnTop = false;
            }

            SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | 0x0010);
        }

        public void SetTheme(string theme)
        {
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme == "Light" ? ElementTheme.Light : (theme == "Dark" ? ElementTheme.Dark : ElementTheme.Default);
            }
            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();
        }
        #endregion

        #region Widget Sizing & Paging Engine

        private void WidgetSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioMenuFlyoutItem item && item.Tag is string size)
            {
                _currentSize = size;
                ApplyWidgetSize();
            }
        }

        private void ApplyWidgetSize()
        {
            var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
            var appWindow = AppWindow.GetFromWindowId(windowId);

            string savedPos = SettingsEngine.GetWidgetPosition(ParentCategory.Name);
            int startX = appWindow.Position.X;
            int startY = appWindow.Position.Y;

            if (!string.IsNullOrEmpty(savedPos))
            {
                var coords = savedPos.Split(',');
                if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
                {
                    startX = x;
                    startY = y;
                }
            }

            int newWidth = 492;
            int newHeight = 240;
            int appCount = SelectedTabApps?.Count ?? 1;
            if (appCount == 0) appCount = 1;

            if (_currentSize == "Custom")
            {
                newWidth = 32 + (3 * 92);
                newHeight = 136 + (2 * 110);
                _appsPerPage = 3 * 2;

                ResizeHandle.Visibility = Visibility.Visible;

                if (MainAppGrid.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
                {
                    wrapGrid.Orientation = Orientation.Vertical;
                    wrapGrid.MaximumRowsOrColumns = 2;
                }
                ScrollViewer.SetHorizontalScrollMode(MainAppGrid, ScrollMode.Enabled);
                ScrollViewer.SetVerticalScrollMode(MainAppGrid, ScrollMode.Disabled);
            }
            else if (_currentSize == "Medium")
            {
                newWidth = 492;
                newHeight = 240;
                _appsPerPage = 5;

                ResizeHandle.Visibility = Visibility.Collapsed;

                if (MainAppGrid.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
                {
                    wrapGrid.Orientation = Orientation.Vertical;
                    wrapGrid.MaximumRowsOrColumns = 1;
                }
                ScrollViewer.SetHorizontalScrollMode(MainAppGrid, ScrollMode.Enabled);
                ScrollViewer.SetVerticalScrollMode(MainAppGrid, ScrollMode.Disabled);
            }
            else if (_currentSize == "Large")
            {
                newWidth = 492;
                int rowCount = (int)Math.Ceiling((double)appCount / 5.0);
                newHeight = 90 + (rowCount * 104) + 20;
                if (newHeight > 600) newHeight = 600;

                ResizeHandle.Visibility = Visibility.Collapsed;

                if (MainAppGrid.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
                {
                    wrapGrid.Orientation = Orientation.Horizontal;
                    wrapGrid.MaximumRowsOrColumns = 5;
                }
                ScrollViewer.SetHorizontalScrollMode(MainAppGrid, ScrollMode.Disabled);
                ScrollViewer.SetVerticalScrollMode(MainAppGrid, ScrollMode.Auto);
            }

            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(startX, startY, newWidth, newHeight));
            UpdatePaging();
        }

        private void UpdatePaging()
        {
            if (_currentSize == "Large" || SelectedTabApps == null)
            {
                BottomNavigationGrid.Visibility = Visibility.Collapsed;
                return;
            }

            int pageCount = (int)Math.Ceiling((double)SelectedTabApps.Count / _appsPerPage);

            if (pageCount <= 1)
            {
                BottomNavigationGrid.Visibility = Visibility.Collapsed;
                return;
            }

            BottomNavigationGrid.Visibility = Visibility.Visible;
            PageDots.Clear();
            for (int i = 0; i < pageCount; i++)
            {
                PageDots.Add(new WidgetPageDot
                {
                    PageIndex = i,
                    IndicatorSize = (i == _currentPage) ? 8.0 : 6.0,
                    IndicatorOpacity = (i == _currentPage) ? 1.0 : 0.4
                });
            }

            ScrollToPage(_currentPage);
        }

        private void ScrollToPage(int pageIndex)
        {
            _currentPage = pageIndex;
            foreach (var dot in PageDots)
            {
                dot.IndicatorSize = (dot.PageIndex == _currentPage) ? 8.0 : 6.0;
                dot.IndicatorOpacity = (dot.PageIndex == _currentPage) ? 1.0 : 0.4;
            }

            var sv = GetScrollViewer(MainAppGrid);
            if (sv != null)
            {
                int appsPerRow = _appsPerPage / (MainAppGrid.ItemsPanelRoot is ItemsWrapGrid wg ? wg.MaximumRowsOrColumns : 1);
                double scrollOffset = _currentPage * appsPerRow * 92.0;
                sv.ChangeView(scrollOffset, null, null, true);
            }
        }

        private void PageLeftBtn_Click(object? sender, RoutedEventArgs? e)
        {
            if (_currentPage > 0) ScrollToPage(_currentPage - 1);
        }

        private void PageRightBtn_Click(object? sender, RoutedEventArgs? e)
        {
            int maxPage = (int)Math.Ceiling((double)(SelectedTabApps?.Count ?? 0) / _appsPerPage) - 1;
            if (_currentPage < maxPage) ScrollToPage(_currentPage + 1);
        }

        private void PageDotIndicator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int pageIndex) ScrollToPage(pageIndex);
        }

        private void MainAppGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (_currentSize == "Large") return;

            var delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
            if (delta < 0) PageRightBtn_Click(null, null);
            else if (delta > 0) PageLeftBtn_Click(null, null);

            e.Handled = true;
        }

        private void BottomNavigationGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
            if (delta < 0) PageRightBtn_Click(null, null);
            else if (delta > 0) PageLeftBtn_Click(null, null);
            e.Handled = true;
        }

        private void DotIndicator_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is Ellipse dot && dot.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = 1.35; scale.ScaleY = 1.35;
            }
        }

        private void DotIndicator_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is Ellipse dot && dot.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = 1.0; scale.ScaleY = 1.0;
            }
        }

        private ScrollViewer? GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

        #region Custom Drag Snapping Engine

        private bool _isResizing = false;
        private POINT _resizeStartScreenPt;
        private int _resizeStartWidth;
        private int _resizeStartHeight;

        private void ResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_currentSize != "Custom") return;

            _isResizing = true;
            ResizeHandle.CapturePointer(e.Pointer);

            var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this)));
            _resizeStartWidth = appWindow.Size.Width;
            _resizeStartHeight = appWindow.Size.Height;

            GetCursorPos(out _resizeStartScreenPt);
            e.Handled = true;
        }

        private void ResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isResizing) return;

            GetCursorPos(out POINT currentScreenPt);
            int diffX = currentScreenPt.X - _resizeStartScreenPt.X;
            int diffY = currentScreenPt.Y - _resizeStartScreenPt.Y;

            int targetWidth = Math.Max(200, _resizeStartWidth + diffX);
            int targetHeight = Math.Max(150, _resizeStartHeight + diffY);

            int cols = Math.Max(1, (int)Math.Round((targetWidth - 32.0) / 92.0));
            int rows = Math.Max(1, (int)Math.Round((targetHeight - 136.0) / 110.0));

            int snappedWidth = 32 + (cols * 92);
            int snappedHeight = 136 + (rows * 110);

            var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this)));

            if (appWindow.Size.Width != snappedWidth || appWindow.Size.Height != snappedHeight)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(snappedWidth, snappedHeight));

                _appsPerPage = cols * rows;

                if (MainAppGrid.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
                {
                    wrapGrid.MaximumRowsOrColumns = rows;
                }
                UpdatePaging();
            }

            e.Handled = true;
        }

        private void ResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                ResizeHandle.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        #endregion

        #region Standard Action Handlers & Folder Overlay

        private void CloseWidget_Click(object sender, RoutedEventArgs e) => this.Close();

        private void AppGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app && !string.IsNullOrEmpty(app.ExecutablePath))
            {
                if (app.ExecutablePath == "PINNED_FOLDER")
                {
                    OpenPinnedFolder(app);
                    return;
                }

                LaunchApp(app, false);
            }
        }

        private void AppCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is FrameworkElement element && (element.Tag as AppItem ?? element.DataContext as AppItem) is AppItem app)
            {
                if (app == _placeholderItem) return;

                MenuFlyout flyout = new MenuFlyout();

                var unpinItem = new MenuFlyoutItem { Text = ResourceString.GetString("UnpinFromWidget"), Icon = new FontIcon { Glyph = "\xE141" } };
                unpinItem.Click += (s, args) =>
                {
                    SelectedTabApps?.Remove(app);
                    _saveCallback?.Invoke();
                    ApplyWidgetSize();
                };
                flyout.Items.Add(unpinItem);

                if (!app.IsUwp && app.ExecutablePath != "PINNED_FOLDER")
                {
                    var adminItem = new MenuFlyoutItem { Text = ResourceString.GetString("RunAsAdmin"), Icon = new FontIcon { Glyph = "\xE7EF" } };
                    adminItem.Click += (s, args) => LaunchApp(app, true);
                    flyout.Items.Add(adminItem);

                    var locItem = new MenuFlyoutItem { Text = ResourceString.GetString("OpenFileLocation"), Icon = new FontIcon { Glyph = "\xE8DA" } };
                    locItem.Click += (s, args) =>
                    {
                        try
                        {
                            string? dir = System.IO.Path.GetDirectoryName(app.ExecutablePath);
                            if (!string.IsNullOrEmpty(dir)) Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
                        }
                        catch { }
                    };
                    flyout.Items.Add(locItem);
                }

                flyout.Items.Add(new MenuFlyoutSeparator());

                var closeWidgetItem = new MenuFlyoutItem { Text = ResourceString.GetString("CloseWidget"), Icon = new FontIcon { Glyph = "\xE711" } };
                closeWidgetItem.Click += (s, args) => this.Close();
                flyout.Items.Add(closeWidgetItem);

                flyout.ShowAt(element, e.GetPosition(element));
            }
        }

        private void LaunchApp(AppItem app, bool runAsAdmin)
        {
            try
            {
                var psi = new ProcessStartInfo { UseShellExecute = true };
                if (app.IsUwp)
                {
                    psi.FileName = "explorer.exe";
                    psi.Arguments = $@"shell:appsFolder\{app.ExecutablePath}";
                }
                else
                {
                    psi.FileName = app.ExecutablePath;
                    if (runAsAdmin) psi.Verb = "runas";
                }
                Process.Start(psi);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void AppCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border) border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255));
        }

        private void AppCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is AppItem app) border.Background = app.TileBrush ?? new SolidColorBrush(Colors.Transparent);
        }

        private void OpenPinnedFolder(AppItem app)
        {
            _activeFolderApp = app;
            if (OverlayFolderNameBox != null)
                OverlayFolderNameBox.Text = string.IsNullOrWhiteSpace(app.Name) || app.Name == " " ? ResourceString.GetString("FolderNameFallback") : app.Name;

            if (OverlayFolderGrid != null)
                OverlayFolderGrid.ItemsSource = app.FolderApps;

            if (PinnedFolderOverlay != null)
            {
                PinnedFolderOverlay.Visibility = Visibility.Visible;
                FolderOverlayEnterAnimation.Begin();
            }
        }

        private async void CloseFolderOverlay()
        {
            if (PinnedFolderOverlay == null || PinnedFolderOverlay.Visibility == Visibility.Collapsed) return;

            FolderOverlayExitAnimation.Begin();

            SaveOverlayFolderName();
            _activeFolderApp = null;

            await Task.Delay(150);
            PinnedFolderOverlay.Visibility = Visibility.Collapsed;
        }

        private void CloseFolderOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (object.ReferenceEquals(e.OriginalSource, PinnedFolderOverlay)) CloseFolderOverlay();
        }

        private void OverlayFolderGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app)
            {
                LaunchApp(app, false);
                CloseFolderOverlay();
            }
        }

        private void OverlayFolderNameBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                SaveOverlayFolderName();
                OverlayFolderGrid.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        private void OverlayFolderNameBox_LostFocus(object sender, RoutedEventArgs e) => SaveOverlayFolderName();

        private void SaveOverlayFolderName()
        {
            if (_activeFolderApp != null && OverlayFolderNameBox.Text != _activeFolderApp.Name)
            {
                _activeFolderApp.Name = OverlayFolderNameBox.Text;
                _saveCallback?.Invoke();
            }
        }

        #endregion

        #region Unified Dragging Engine (Window & Items)

        private bool _isWindowDragging = false;
        private int _windowDragOffsetX;
        private int _windowDragOffsetY;

        private AppItem? _draggedAppItem;
        private bool _isAppDragging = false;
        private Point _dragStartPoint;
        private FrameworkElement? _dragGhost;

        private void Widget_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed && !_isAppDragging)
            {
                _isWindowDragging = true;
                (sender as UIElement)?.CapturePointer(e.Pointer);

                var hwnd = WindowNative.GetWindowHandle(this);
                GetWindowRect(hwnd, out RECT rect);
                GetCursorPos(out POINT pt);

                _windowDragOffsetX = pt.X - rect.Left;
                _windowDragOffsetY = pt.Y - rect.Top;
                e.Handled = true;
            }
        }

        private void AppCard_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && (element.Tag as AppItem ?? element.DataContext as AppItem) is AppItem app)
            {
                if (app == _placeholderItem || !e.GetCurrentPoint(element).Properties.IsLeftButtonPressed) return;

                _draggedAppItem = app;
                _dragStartPoint = e.GetCurrentPoint(MenuContainer).Position;
                _isAppDragging = false;
                _isWindowDragging = false;

                MenuContainer.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void MenuContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isWindowDragging)
            {
                GetCursorPos(out POINT ptScreen);
                var hwnd = WindowNative.GetWindowHandle(this);
                SetWindowPos(hwnd, IntPtr.Zero, ptScreen.X - _windowDragOffsetX, ptScreen.Y - _windowDragOffsetY, 0, 0, 0x0015);
                return;
            }

            if (_draggedAppItem == null || SelectedTabApps == null) return;

            var pt = e.GetCurrentPoint(MenuContainer).Position;

            if (!_isAppDragging)
            {
                if (Math.Abs(pt.X - _dragStartPoint.X) > 4 || Math.Abs(pt.Y - _dragStartPoint.Y) > 4)
                {
                    _isAppDragging = true;
                    CreateDragGhost();

                    int idx = SelectedTabApps.IndexOf(_draggedAppItem);
                    if (idx != -1) SelectedTabApps[idx] = _placeholderItem;
                }
            }

            if (_isAppDragging && _dragGhost != null)
            {
                Canvas.SetLeft(_dragGhost, pt.X - 40);
                Canvas.SetTop(_dragGhost, pt.Y - 48);

                int targetIndex = GetHoveredDropTargetIndex(pt);
                if (targetIndex >= 0)
                {
                    int currentIndex = SelectedTabApps.IndexOf(_placeholderItem);
                    if (currentIndex != targetIndex)
                    {
                        SelectedTabApps.Remove(_placeholderItem);
                        if (targetIndex > SelectedTabApps.Count) targetIndex = SelectedTabApps.Count;
                        SelectedTabApps.Insert(targetIndex, _placeholderItem);
                    }
                }
            }
        }

        private void MenuContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isWindowDragging)
            {
                _isWindowDragging = false;
                MenuContainer.ReleasePointerCapture(e.Pointer);
                e.Handled = true;

                var hwnd = WindowNative.GetWindowHandle(this);
                GetWindowRect(hwnd, out RECT rect);
                SettingsEngine.SetWidgetPosition(ParentCategory.Name, $"{rect.Left},{rect.Top}");

                return;
            }

            if (_draggedAppItem != null && SelectedTabApps != null)
            {
                try { MenuContainer.ReleasePointerCapture(e.Pointer); } catch { }

                if (_isAppDragging)
                {
                    if (_dragGhost != null)
                    {
                        DragCanvas.Children.Remove(_dragGhost);
                        _dragGhost = null;
                    }

                    int idx = SelectedTabApps.IndexOf(_placeholderItem);
                    if (idx != -1)
                    {
                        SelectedTabApps[idx] = _draggedAppItem;
                        _saveCallback?.Invoke();
                        ApplyWidgetSize();
                    }
                    else
                    {
                        SelectedTabApps.Add(_draggedAppItem);
                    }
                }
                else
                {
                    if (_draggedAppItem.ExecutablePath == "PINNED_FOLDER") OpenPinnedFolder(_draggedAppItem);
                    else LaunchApp(_draggedAppItem, false);
                }

                _draggedAppItem = null;
                _isAppDragging = false;
                e.Handled = true;
            }
        }

        private int GetHoveredDropTargetIndex(Point pointerPos)
        {
            if (MainAppGrid == null || MainAppGrid.Visibility != Visibility.Visible) return -1;

            var transform = MainAppGrid.TransformToVisual(MenuContainer);
            var bounds = transform.TransformBounds(new Rect(0, 0, MainAppGrid.ActualWidth, MainAppGrid.ActualHeight));

            if (bounds.Contains(pointerPos))
            {
                int targetIndex = MainAppGrid.Items.Count;
                double closestDist = double.MaxValue;
                int visibleCount = 0;

                for (int i = 0; i < MainAppGrid.Items.Count; i++)
                {
                    if (MainAppGrid.Items[i] == _placeholderItem) continue;

                    if (MainAppGrid.ContainerFromIndex(i) is FrameworkElement itemContainer)
                    {
                        var itemTransform = itemContainer.TransformToVisual(MenuContainer);
                        var itemBounds = itemTransform.TransformBounds(new Rect(0, 0, itemContainer.ActualWidth, itemContainer.ActualHeight));

                        var centerX = itemBounds.X + (itemBounds.Width / 2);
                        var centerY = itemBounds.Y + (itemBounds.Height / 2);

                        double dist = Math.Pow(pointerPos.X - centerX, 2) + Math.Pow(pointerPos.Y - centerY, 2);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            targetIndex = pointerPos.X < centerX ? visibleCount : visibleCount + 1;
                        }
                        visibleCount++;
                    }
                }
                return targetIndex;
            }
            return -1;
        }

        private void CreateDragGhost()
        {
            if (_draggedAppItem == null) return;

            var panel = new StackPanel
            {
                Width = 80,
                Height = 96,
                Spacing = 4,
                Padding = new Thickness(4, 8, 4, 8),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(120, 200, 200, 200))
            };

            if (_draggedAppItem.HasIcon == Visibility.Visible)
            {
                panel.Children.Add(new Image
                {
                    Source = _draggedAppItem.IconSource,
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform { ScaleX = _draggedAppItem.IconScale, ScaleY = _draggedAppItem.IconScale }
                });
            }
            else
            {
                panel.Children.Add(new FontIcon { Glyph = _draggedAppItem.FallbackGlyph, FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center });
            }

            panel.Children.Add(new TextBlock
            {
                Text = _draggedAppItem.Name,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2
            });

            _dragGhost = panel;
            DragCanvas.Children.Add(_dragGhost);
        }

        #endregion
    }
}