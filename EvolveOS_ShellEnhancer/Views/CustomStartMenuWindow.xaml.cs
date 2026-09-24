// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System.IO;
using Windows.System;
using WinRT.Interop;
using EvolveOS_ShellEnhancer.ViewModels;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class CustomStartMenuWindow : Window
    {
        #region Fields & Properties
        public CustomStartMenuViewModel ViewModel { get; } = new();

        public DisplayArea? TargetDisplayArea { get; set; }

        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private bool _isVisible = false;

        private string _currentAlignment = "Center";
        private string _currentPosition = "Bottom";
        private string _currentStyle = "SplitStandard";

        public static bool EnableAnimations { get; set; } = true;
        public static string AnimationStyle { get; set; } = "Standard";
        public static double AnimationSpeed { get; set; } = 1.0;

        public static bool ShowPowerSleep { get; set; } = true;
        public static bool ShowPowerRestartBios { get; set; } = false;
        public static bool ShowPowerLogOff { get; set; } = false;
        public static bool ShowRecentDocs { get; set; } = false;

        public ObservableCollection<StartMenuPage> Pages => ViewModel.Pages;
        public ObservableCollection<AppCategory> PinnedCategories => ViewModel.PinnedCategories;
        public ObservableCollection<AppItem> RecentDocsCollection => ViewModel.RecentDocsCollection;
        public ObservableCollection<AppItem> AllAppsCollection => ViewModel.AllAppsCollection;
        public ObservableCollection<AppItem> SearchResultsCollection => ViewModel.SearchResultsCollection;
        public ObservableCollection<ShortcutItem> StartMenuShortcuts => ViewModel.StartMenuShortcuts;

        private string _currentSearchFilter = "Apps";
        private bool _isShowingAllApps = false;
        private AppItem? _currentSearchItem;

        private AccountCardWindow? _activeAccountCardWindow;
        private DateTime _lastAccountCardCloseTime = DateTime.MinValue;
        private string _currentUserEmail = string.Empty;
        private string _currentAccountType = "Local Account";
        private bool _ignoreDeactivation = false;

        private bool _isRecentDocsExpanded = false;

        private DateTime _lastPageFlipTime = DateTime.MinValue;
        private const double EdgeScrollThreshold = 60.0;
        private const int PageFlipDelayMs = 700;
        #endregion

        #region Initialization & Data Loading
        public CustomStartMenuWindow()
        {
            this.InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            string savedTheme = SettingsEngine.Shell_AppTheme ?? "Default";
            SetTheme(savedTheme);

            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            Win32Helper.RemoveWindowBorders(_hWnd);
            TaskbarOverlayManager.ApplyWidgetStyles(_hWnd);

            _appWindow.Hide();

            this.Activated += OnWindowActivated;
            PagesFlipView.Loaded += PagesFlipView_Loaded;

            ViewModel.OnUserProfileLoaded = (name, pic, email, accountType) =>
            {
                _currentUserEmail = email;
                _currentAccountType = accountType;
                if (UnifiedProfileName != null) UnifiedProfileName.Text = name;
                if (UnifiedProfilePic != null)
                {
                    UnifiedProfilePic.DisplayName = name;
                    if (pic != null) UnifiedProfilePic.ProfilePicture = pic;
                }
                bool enableProfile = SettingsEngine.Shell_StartMenuProfileClick;
                if (ProfileButton != null)
                {
                    ProfileButton.IsHitTestVisible = enableProfile;
                    ProfileButton.IsEnabled = enableProfile;
                }
            };

            ViewModel.OnAppsDataLoaded = () =>
            {
                UpdatePageIndicators(0);
            };

            ViewModel.InitializeAppWatchers(DispatcherQueue);
            ViewModel.LoadAppsData(DispatcherQueue);
            ViewModel.LoadUserProfile(DispatcherQueue);
            ViewModel.UpdateShortcuts(SettingsEngine.Shell_StartMenuShortcuts ?? string.Empty);
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

            ViewModel.LoadUserProfile(DispatcherQueue);
        }

        public void LoadRecentDocuments()
        {
            ViewModel.LoadRecentDocuments(ShowRecentDocs);

            if (Pages.Count > 0)
            {
                UpdateRecentDocsView(Pages[0]);
            }
        }

        public void UpdateShortcuts(string payload)
        {
            ViewModel.UpdateShortcuts(payload);
        }


        private void UpdateRecentDocsView(StartMenuPage page)
        {
            page.RecentDocsCollection.Clear();

            foreach (var item in ViewModel.AllRecentDocs)
            {
                page.RecentDocsCollection.Add(item);
            }

            if (page.RecentDocsCollection.Count > 0)
            {
                page.RecentDocsVisibility = Visibility.Visible;
                page.RecentDocsChevronAngle = _isRecentDocsExpanded ? 180.0 : 0.0;
            }
            else
            {
                page.RecentDocsVisibility = Visibility.Collapsed;
            }
        }

        private void RecentDocsMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is StartMenuPage page)
            {
                _isRecentDocsExpanded = !_isRecentDocsExpanded;

                if (btn.Content is FontIcon icon && icon.RenderTransform is RotateTransform transform)
                    FactoryAnimation.AnimateRotation(transform, _isRecentDocsExpanded ? 180 : 0);

                if (btn.Parent is Grid headerGrid && headerGrid.Parent is StackPanel stackPanel && stackPanel.Children[1] is GridView recentDocsGrid)
                {
                    FactoryAnimation.AnimatePanelExpansion(recentDocsGrid, _isRecentDocsExpanded, 116);

                    if (_isRecentDocsExpanded)
                    {
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            await Task.Delay(260);
                            var scrollViewer = FindVisualParent<ScrollViewer>(btn);
                            if (scrollViewer != null)
                            {
                                scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null, false);
                            }
                        });
                    }
                }
            }
        }
        #endregion

        #region Window & Layout Management
        public void ToggleVisibility()
        {
            if (!ViewModel.IsDataLoaded)
            {
                ViewModel.LoadAppsData(DispatcherQueue);
            }

            if (_isVisible) HideMenu();
            else ShowMenu();
        }

        public void SetAlignment(string alignment)
        {
            _currentAlignment = alignment;
            if (_isVisible) ShowMenu();
        }

        public void SetPosition(string position)
        {
            _currentPosition = position;
            if (_isVisible) ShowMenu();
        }

        public void PositionOnDisplay(DisplayArea area)
        {
            TargetDisplayArea = area;
        }

        public void SetStyle(string style)
        {
            _currentStyle = style;

            bool isStandard = (_currentStyle == "Standard" || _currentStyle == "SplitStandard");
            bool isGrouped = (_currentStyle == "Compact" || _currentStyle == "SplitGrouped");

            DesignSplitStandard.Visibility = isStandard ? Visibility.Visible : Visibility.Collapsed;
            DesignSplitGrouped.Visibility = isGrouped ? Visibility.Visible : Visibility.Collapsed;

            if (_isVisible) ShowMenu();
        }

        private void UpdatePowerMenuVisibility()
        {
            var sleepVis = ShowPowerSleep ? Visibility.Visible : Visibility.Collapsed;
            var biosVis = ShowPowerRestartBios ? Visibility.Visible : Visibility.Collapsed;
            var logOffVis = ShowPowerLogOff ? Visibility.Visible : Visibility.Collapsed;

            if (PowerItemSleep1 != null) PowerItemSleep1.Visibility = sleepVis;
            if (PowerItemSleep2 != null) PowerItemSleep2.Visibility = sleepVis;

            if (PowerItemRestartBios1 != null) PowerItemRestartBios1.Visibility = biosVis;
            if (PowerItemRestartBios2 != null) PowerItemRestartBios2.Visibility = biosVis;

            if (PowerItemLogOff1 != null) PowerItemLogOff1.Visibility = logOffVis;
            if (PowerItemLogOff2 != null) PowerItemLogOff2.Visibility = logOffVis;
        }

        private void ShowMenu()
        {
            var displayArea = TargetDisplayArea ?? DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);

            UpdatePowerMenuVisibility();

            LoadRecentDocuments();

            int windowWidth = 780;
            int windowHeight = 680;

            int taskbarOffset = 60;
            int margin = 16;

            int x = 0; int y = 0;
            string targetPos = TaskbarManager.GetPositionForDisplay(displayArea.DisplayId.Value.ToString());

            switch (targetPos)
            {
                case "Top":
                    y = displayArea.OuterBounds.Y + taskbarOffset;
                    x = (_currentAlignment == "Center") ? displayArea.OuterBounds.X + (displayArea.OuterBounds.Width - windowWidth) / 2 : displayArea.OuterBounds.X + margin;
                    break;
                case "Left":
                    x = displayArea.OuterBounds.X + taskbarOffset;
                    y = (_currentAlignment == "Center") ? displayArea.OuterBounds.Y + (displayArea.OuterBounds.Height - windowHeight) / 2 : displayArea.OuterBounds.Y + margin;
                    break;
                case "Right":
                    x = displayArea.OuterBounds.X + displayArea.OuterBounds.Width - windowWidth - taskbarOffset;
                    y = (_currentAlignment == "Center") ? displayArea.OuterBounds.Y + (displayArea.OuterBounds.Height - windowHeight) / 2 : displayArea.OuterBounds.Y + margin;
                    break;
                case "Bottom":
                default:
                    y = displayArea.OuterBounds.Y + displayArea.OuterBounds.Height - windowHeight - taskbarOffset;
                    x = (_currentAlignment == "Center") ? displayArea.OuterBounds.X + (displayArea.OuterBounds.Width - windowWidth) / 2 : displayArea.OuterBounds.X + margin;
                    break;
            }

            if (EnableAnimations)
            {
                int startX = x, startY = y;
                if (targetPos == "Top") startY = y - windowHeight - 15;
                else if (targetPos == "Left") startX = x - windowWidth - 15;
                else if (targetPos == "Right") startX = x + windowWidth + 15;
                else startY = y + windowHeight + 15;

                _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(startX, startY, windowWidth, windowHeight));
                _appWindow.Show();

                this.Activate();
                Win32Helper.SetForegroundWindow(_hWnd);

                AvatarPopup.IsOpen = true;

                SetWindowPos(_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

                _isVisible = true;

                TaskbarOverlayManager.EnsureTopmost(_hWnd);
                TaskbarManager.EnsureAllTaskbarsTopmost();

                FactoryAnimation.PlayStartMenuAnimation(
                    _appWindow, AnimationStyle, AnimationSpeed, true,
                    startX, startY, windowWidth, windowHeight,
                    x, y, windowWidth, windowHeight,
                    null);
            }
            else
            {
                _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, windowWidth, windowHeight));
                _appWindow.Show();

                this.Activate();
                Win32Helper.SetForegroundWindow(_hWnd);

                AvatarPopup.IsOpen = true;

                SetWindowPos(_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

                _isVisible = true;
                TaskbarOverlayManager.EnsureTopmost(_hWnd);
                TaskbarManager.EnsureAllTaskbarsTopmost();
            }
        }

        private void HideMenu()
        {
            if (!_isVisible) return;

            _isVisible = false;
            App.LastStartMenuCloseTime = DateTime.Now;

            if (EnableAnimations)
            {
                int startX = _appWindow.Position.X;
                int startY = _appWindow.Position.Y;
                int width = _appWindow.Size.Width;
                int height = _appWindow.Size.Height;

                int targetX = startX;
                int targetY = startY;

                var displayArea = TargetDisplayArea ?? DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
                string targetPos = TaskbarManager.GetPositionForDisplay(displayArea.DisplayId.Value.ToString());

                if (targetPos == "Top") targetY = startY - height - 15;
                else if (targetPos == "Left") targetX = startX - width - 15;
                else if (targetPos == "Right") targetX = startX + width + 15;
                else targetY = startY + height + 15;

                FactoryAnimation.PlayStartMenuAnimation(
                    _appWindow, AnimationStyle, AnimationSpeed, false,
                    startX, startY, width, height,
                    targetX, targetY, width, height,
                    () =>
                    {
                        _appWindow.Hide();
                        AvatarPopup.IsOpen = false;

                        _activeAccountCardWindow?.Close();
                        _activeAccountCardWindow = null;
                    });
            }
            else
            {
                _appWindow.Hide();
                AvatarPopup.IsOpen = false;

                _activeAccountCardWindow?.Close();
                _activeAccountCardWindow = null;
            }
        }
        #endregion

        #region UI Event Handlers
        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                if (_ignoreDeactivation) return;
                HideMenu();
            }
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            HideMenu();
        }

        private void MenuContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(MenuContainer).Properties.IsRightButtonPressed) return;
            if (e.OriginalSource is Image || e.OriginalSource is TextBlock || e.OriginalSource is FontIcon) return;
            e.Handled = true;
        }

        private void DismissLayer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            HideMenu();
        }

        private void PowerShutdown_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true });
            HideMenu();
        }

        private void PowerRestart_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true });
            HideMenu();
        }

        private void PowerSleep_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0") { CreateNoWindow = true });
            HideMenu();
        }

        private void PowerLogOff_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("shutdown", "/l") { CreateNoWindow = true });
            HideMenu();
        }

        private void PowerRestartBios_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("shutdown", "/r /fw /t 0") { CreateNoWindow = true });
            HideMenu();
        }

        private async void ActionChangeAccount_Click(object sender, RoutedEventArgs e)
        {
            try { await Launcher.LaunchUriAsync(new Uri("ms-settings:accounts")); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
            HideMenu();
        }

        private void ActionLock_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", "user32.dll,LockWorkStation") { CreateNoWindow = true });
            HideMenu();
        }

        private void ActionSignOut_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("shutdown", "/l") { CreateNoWindow = true });
            HideMenu();
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsEngine.Shell_StartMenuProfileClick)
            {
                if (_activeAccountCardWindow != null)
                {
                    _activeAccountCardWindow.Close();
                    return;
                }

                if ((DateTime.Now - _lastAccountCardCloseTime).TotalMilliseconds < 200) return;

                _ignoreDeactivation = true;

                int cardWidth = 320;
                int offsetX = _appWindow.Position.X + _appWindow.Size.Width - cardWidth - 16;
                int offsetY = _appWindow.Position.Y + 60;

                _activeAccountCardWindow = new AccountCardWindow(
                    targetX: offsetX,
                    targetY: offsetY,
                    name: UnifiedProfileName.Text,
                    accountType: _currentAccountType,
                    email: _currentUserEmail,
                    profilePic: UnifiedProfilePic.ProfilePicture,
                    parentHwnd: _hWnd,
                    onDismiss: (clickedOutsideBoth) =>
                    {
                        _activeAccountCardWindow = null;
                        _ignoreDeactivation = false;
                        if (clickedOutsideBoth) HideMenu();
                    });

                _activeAccountCardWindow.Activate();
            }
        }

        private void ProfileButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (SettingsEngine.Shell_StartMenuProfileClick) ProfileGrowStoryboard.Begin();
        }

        private void ProfileButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (SettingsEngine.Shell_StartMenuProfileClick) ProfileShrinkStoryboard.Begin();
        }

        private async void Shortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetPath)
            {
                try
                {
                    if (targetPath == "ms-settings:")
                    {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:"));
                    }
                    else if (targetPath == "Standard::Run")
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", "shell:::{2559a1f3-21d7-11d4-bdaf-00c04f60b9f0}") { UseShellExecute = true });
                    }
                    else if (targetPath.Equals("control.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start(new ProcessStartInfo("control.exe") { UseShellExecute = true });
                    }
                    else if (targetPath.StartsWith("Standard::"))
                    {
                        string folder = targetPath.Replace("Standard::", "");
                        string? path = folder switch
                        {
                            "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "Pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                            "Music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                            "Downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                            _ => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                        };

                        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                            await Launcher.LaunchFolderPathAsync(path);
                    }
                    else
                    {
                        if (Directory.Exists(targetPath))
                            await Launcher.LaunchFolderPathAsync(targetPath);
                        else if (File.Exists(targetPath))
                            Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open shortcut: {ex.Message}");
                }
                HideMenu();
            }
        }

        private void BtnAllApps_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                _isShowingAllApps = !_isShowingAllApps;

                if (_isShowingAllApps)
                {
                    btn.Content = LocalizationService.Instance.GetString("StartMenu_BackToPinned");

                    if (PagesFlipView != null) PagesFlipView.Visibility = Visibility.Collapsed;
                    if (BottomNavigationGrid != null) BottomNavigationGrid.Visibility = Visibility.Collapsed;
                    if (SearchAndAllAppsGrid != null)
                    {
                        SearchAndAllAppsGrid.Visibility = Visibility.Visible;
                        SearchAndAllAppsGrid.ItemsSource = AllAppsCollection;
                    }
                }
                else
                {
                    btn.Content = LocalizationService.Instance.GetString("StartMenu_AllApps");

                    if (PagesFlipView != null) PagesFlipView.Visibility = Visibility.Visible;
                    if (BottomNavigationGrid != null) BottomNavigationGrid.Visibility = Visibility.Visible;
                    if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            PinnedCategories.Add(new AppCategory { Name = "New Section" });
            ViewModel.SaveStartMenuPins();
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem btn && btn.Tag is AppCategory cat)
            {
                var page = Pages.FirstOrDefault(p => p.PinnedCategories.Contains(cat));
                if (page != null)
                {
                    page.PinnedCategories.Remove(cat);
                    ViewModel.SaveStartMenuPins();
                }
            }
        }

        private void CategoryName_LostFocus(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveStartMenuPins();
        }

        private void CategoryName_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                this.Content.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        private void PagesFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PagesFlipView != null && PagesFlipView.SelectedIndex >= 0)
            {
                UpdatePageIndicators(PagesFlipView.SelectedIndex);
            }
        }

        private void UpdatePageIndicators(int selectedIndex)
        {
            for (int i = 0; i < Pages.Count; i++)
            {
                Pages[i].IndicatorOpacity = (i == selectedIndex) ? 1.0 : 0.3;
            }
        }

        private void PageLeftBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PagesFlipView != null && PagesFlipView.SelectedIndex > 0)
            {
                PagesFlipView.SelectedIndex -= 1;
            }
        }

        private void PageRightBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PagesFlipView != null && PagesFlipView.SelectedIndex >= 0 && PagesFlipView.SelectedIndex < Pages.Count - 1)
            {
                PagesFlipView.SelectedIndex += 1;
            }
        }

        private void PageDotIndicator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int targetIndex)
            {
                if (PagesFlipView != null && targetIndex >= 0 && targetIndex < Pages.Count)
                {
                    PagesFlipView.SelectedIndex = targetIndex;
                }
            }
        }

        private void PagesFlipView_Loaded(object sender, RoutedEventArgs e)
        {
            PagesFlipView.ApplyTemplate();
            HideAllFlipViewButtons(PagesFlipView);
        }

        private void PagesFlipView_LayoutUpdated(object sender, object e)
        {
            HideAllFlipViewButtons(PagesFlipView);
        }

        private void PagesFlipView_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            HideAllFlipViewButtons(PagesFlipView);
        }

        private void HideAllFlipViewButtons(DependencyObject parent)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FlipViewItem)
                    continue;

                if (child is FrameworkElement fe)
                {
                    if (fe.Name.Contains("Button", StringComparison.OrdinalIgnoreCase) ||
                        fe.Name.Contains("Prev", StringComparison.OrdinalIgnoreCase) ||
                        fe.Name.Contains("Next", StringComparison.OrdinalIgnoreCase) ||
                        child is ButtonBase)
                    {
                        fe.Visibility = Visibility.Collapsed;
                        fe.IsHitTestVisible = false;
                    }
                }
                HideAllFlipViewButtons(child);
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                    return element;

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void CategoryGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is GridView gridView && gridView.Parent is StackPanel panel)
            {
                if (panel.Children[0] is Grid headerGrid && headerGrid.Children[1] is ToggleButton chevronBtn)
                {
                    int itemsPerRow = (int)(e.NewSize.Width / 92.0);

                    if (gridView.Items.Count > itemsPerRow)
                    {
                        chevronBtn.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        chevronBtn.Visibility = Visibility.Collapsed;
                        if (chevronBtn.IsChecked == true)
                        {
                            chevronBtn.IsChecked = false;
                        }
                    }
                }
            }
        }

        private void CategoryChevron_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton btn)
            {
                if (btn.Content is FontIcon icon && icon.RenderTransform is RotateTransform transform)
                    FactoryAnimation.AnimateRotation(transform, 180);

                if (btn.Parent is Grid headerGrid && headerGrid.Parent is StackPanel panel && panel.Children[1] is GridView gridView)
                {
                    FactoryAnimation.AnimatePanelExpansion(gridView, true, 104);
                }
            }
        }

        private void CategoryChevron_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton btn)
            {
                if (btn.Content is FontIcon icon && icon.RenderTransform is RotateTransform transform)
                    FactoryAnimation.AnimateRotation(transform, 0);

                if (btn.Parent is Grid headerGrid && headerGrid.Parent is StackPanel panel && panel.Children[1] is GridView gridView)
                {
                    FactoryAnimation.AnimatePanelExpansion(gridView, false, 104);
                }
            }
        }

        #endregion

        #region Context Menu Handlers (Pinning / Actions)
        private void ScrollViewer_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.Handled) return;

            if (sender is FrameworkElement element)
            {
                var currentPage = element.DataContext as StartMenuPage ?? Pages.FirstOrDefault();
                if (currentPage == null) return;

                MenuFlyout flyout = new MenuFlyout();

                var addGroupItem = new MenuFlyoutItem { Text = "Add Section" };
                addGroupItem.Icon = new FontIcon { Glyph = "\xE710" };
                addGroupItem.Click += (s, args) =>
                {
                    currentPage.PinnedCategories.Add(new AppCategory { Name = "New Section" });
                    ViewModel.SaveStartMenuPins();
                };
                flyout.Items.Add(addGroupItem);

                var addPageItem = new MenuFlyoutItem { Text = "Add Page" };
                addPageItem.Icon = new FontIcon { Glyph = "\xE7C3" };
                addPageItem.Click += (s, args) =>
                {
                    var newPage = new StartMenuPage { PageIndex = Pages.Count };
                    newPage.PinnedCategories.Add(new AppCategory { Name = "New Section" });

                    Pages.Add(newPage);
                    if (PagesFlipView != null) PagesFlipView.SelectedIndex = Pages.Count - 1;
                    UpdatePageIndicators(Pages.Count - 1);

                    ViewModel.SaveStartMenuPins();
                };
                flyout.Items.Add(addPageItem);

                flyout.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();
                Style flyoutStyle = new Style(typeof(MenuFlyoutPresenter));
                flyoutStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
                flyoutStyle.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(8)));
                flyoutStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255))));
                flyoutStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                flyout.MenuFlyoutPresenterStyle = flyoutStyle;

                flyout.ShowAt(element, e.GetPosition(element));
                e.Handled = true;
            }
        }

        private void AppCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is FrameworkElement element)
            {
                var app = element.Tag as AppItem ?? element.DataContext as AppItem;
                if (app == null || app == _placeholderItem) return;

                if (app.ExecutablePath != null && (app.ExecutablePath.StartsWith("WEB_SEARCH:") || app.ExecutablePath.StartsWith("FILE_SEARCH:")))
                    return;

                MenuFlyout flyout = new MenuFlyout();

                bool isPinnedToStart = Pages.SelectMany(p => p.PinnedCategories).Any(c => c.Apps.Contains(app));
                var pinStartItem = new MenuFlyoutItem
                {
                    Text = isPinnedToStart
                        ? LocalizationService.Instance.GetString("StartMenu_ContextUnpinStart")
                        : LocalizationService.Instance.GetString("StartMenu_ContextPinStart"),
                    Icon = new FontIcon { Glyph = "\xE141" }
                };
                pinStartItem.Click += (s, args) =>
                {
                    if (isPinnedToStart)
                    {
                        foreach (var page in Pages)
                        {
                            foreach (var cat in page.PinnedCategories)
                            {
                                cat.Apps.Remove(app);
                            }
                        }
                    }
                    else
                    {
                        if (Pages.Count > 0 && Pages[0].PinnedCategories.Count > 0)
                        {
                            Pages[0].PinnedCategories.First().Apps.Add(app);
                        }
                        else
                        {
                            if (Pages.Count == 0) Pages.Add(new StartMenuPage { PageIndex = 0 });
                            var defaultCat = new AppCategory { Name = "Pinned" };
                            defaultCat.Apps.Add(app);
                            Pages[0].PinnedCategories.Add(defaultCat);
                        }
                    }

                    ViewModel.SaveStartMenuPins();
                };
                flyout.Items.Add(pinStartItem);
                flyout.Items.Add(new MenuFlyoutSeparator());

                bool isPinnedToTaskbar = ViewModel.IsPinnedToTaskbar(app);
                var pinTaskbarItem = new MenuFlyoutItem
                {
                    Text = isPinnedToTaskbar
                        ? LocalizationService.Instance.GetString("StartMenu_ContextUnpinTaskbar")
                        : LocalizationService.Instance.GetString("StartMenu_ContextPinTaskbar"),
                    Icon = new FontIcon { Glyph = "\xE196" }
                };
                pinTaskbarItem.Click += (s, args) => ViewModel.ToggleTaskbarPin(app, isPinnedToTaskbar);
                flyout.Items.Add(pinTaskbarItem);

                if (!app.IsUwp)
                {
                    flyout.Items.Add(new MenuFlyoutSeparator());

                    var adminItem = new MenuFlyoutItem
                    {
                        Text = LocalizationService.Instance.GetString("StartMenu_ActionRunAsAdmin"),
                        Icon = new FontIcon { Glyph = "\xE7EF" }
                    };
                    adminItem.Click += (s, args) => LaunchApp(app, true);
                    flyout.Items.Add(adminItem);

                    var locItem = new MenuFlyoutItem
                    {
                        Text = LocalizationService.Instance.GetString("StartMenu_ActionOpenLocation"),
                        Icon = new FontIcon { Glyph = "\xE8DA" }
                    };
                    locItem.Click += (s, args) =>
                    {
                        try
                        {
                            string? dir = Path.GetDirectoryName(app.ExecutablePath);
                            if (!string.IsNullOrEmpty(dir))
                                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
                        }
                        catch (Exception ex) { Debug.WriteLine(ex.Message); }
                        HideMenu();
                    };
                    flyout.Items.Add(locItem);
                }

                flyout.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

                Style flyoutStyle = new Style(typeof(MenuFlyoutPresenter));
                flyoutStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
                flyoutStyle.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(8)));
                flyoutStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255))));
                flyoutStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                flyout.MenuFlyoutPresenterStyle = flyoutStyle;

                flyout.ShowAt(element, e.GetPosition(element));
            }
        }
        #endregion

        #region Custom Pointer-Based Drag and Drop Engine
        private AppItem? _draggedAppItem;
        private AppCategory? _sourceCategory;
        private GridView? _sourceGrid;

        private readonly AppItem _placeholderItem = new AppItem { Name = "", FallbackGlyph = "" };

        private FrameworkElement? _dragGhost;
        private Windows.Foundation.Point _dragStartPoint;
        private bool _isAppDragging = false;

        private void AppCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255));
            }
        }

        private void AppCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Colors.Transparent);
            }
        }

        private void AppCard_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var app = element.Tag as AppItem ?? element.DataContext as AppItem;
                if (app == null || app == _placeholderItem) return;

                if (!e.GetCurrentPoint(element).Properties.IsLeftButtonPressed) return;

                _sourceGrid = FindVisualParent<GridView>(element);

                if (_sourceGrid == null || _sourceGrid == SearchAndAllAppsGrid || _sourceGrid.Name == "RecentDocsGrid")
                    return;

                _draggedAppItem = app;
                _sourceCategory = _sourceGrid.DataContext as AppCategory;

                if (_sourceCategory == null)
                    _sourceCategory = Pages.SelectMany(p => p.PinnedCategories).FirstOrDefault(c => c.Apps.Contains(app));

                _dragStartPoint = e.GetCurrentPoint(MenuContainer).Position;
                _isAppDragging = false;

                MenuContainer.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void MenuContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_draggedAppItem == null || _sourceCategory == null) return;

            var pt = e.GetCurrentPoint(MenuContainer).Position;

            if (!_isAppDragging)
            {
                if (Math.Abs(pt.X - _dragStartPoint.X) > 4 || Math.Abs(pt.Y - _dragStartPoint.Y) > 4)
                {
                    _isAppDragging = true;
                    CreateDragGhost();

                    int idx = _sourceCategory.Apps.IndexOf(_draggedAppItem);
                    if (idx != -1)
                    {
                        _sourceCategory.Apps[idx] = _placeholderItem;
                    }
                }
            }

            if (_isAppDragging && _dragGhost != null)
            {
                Canvas.SetLeft(_dragGhost, pt.X - 40);
                Canvas.SetTop(_dragGhost, pt.Y - 48);

                if (PagesFlipView != null)
                {
                    var flipPt = e.GetCurrentPoint(PagesFlipView).Position;

                    if ((DateTime.Now - _lastPageFlipTime).TotalMilliseconds > PageFlipDelayMs)
                    {
                        if (flipPt.X > 0 && flipPt.X < EdgeScrollThreshold && PagesFlipView.SelectedIndex > 0)
                        {
                            PagesFlipView.SelectedIndex -= 1;
                            _lastPageFlipTime = DateTime.Now;
                        }
                        else if (flipPt.X > PagesFlipView.ActualWidth - EdgeScrollThreshold && flipPt.X < PagesFlipView.ActualWidth && PagesFlipView.SelectedIndex < Pages.Count - 1)
                        {
                            PagesFlipView.SelectedIndex += 1;
                            _lastPageFlipTime = DateTime.Now;
                        }
                    }
                }

                var (targetGrid, targetCategory, targetIndex) = GetHoveredDropTarget(pt);

                if (targetGrid != null && targetCategory != null)
                {
                    var currentCategory = Pages.SelectMany(p => p.PinnedCategories).FirstOrDefault(c => c.Apps.Contains(_placeholderItem)) ?? _sourceCategory;
                    int currentIndex = currentCategory.Apps.IndexOf(_placeholderItem);

                    if (currentCategory != targetCategory || currentIndex != targetIndex)
                    {
                        currentCategory.Apps.Remove(_placeholderItem);

                        if (targetIndex > targetCategory.Apps.Count)
                            targetIndex = targetCategory.Apps.Count;

                        if (targetIndex < 0)
                            targetIndex = 0;

                        targetCategory.Apps.Insert(targetIndex, _placeholderItem);
                    }
                }
            }
        }

        private void MenuContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_draggedAppItem != null)
            {
                MenuContainer.ReleasePointerCapture(e.Pointer);

                if (_isAppDragging)
                {
                    if (_dragGhost != null)
                    {
                        DragCanvas.Children.Remove(_dragGhost);
                        _dragGhost = null;
                    }

                    var finalCategory = Pages.SelectMany(p => p.PinnedCategories).FirstOrDefault(c => c.Apps.Contains(_placeholderItem));
                    if (finalCategory != null)
                    {
                        int idx = finalCategory.Apps.IndexOf(_placeholderItem);
                        finalCategory.Apps[idx] = _draggedAppItem;
                    }
                    else
                    {
                        _sourceCategory?.Apps.Add(_draggedAppItem);
                    }

                    ViewModel.SaveStartMenuPins();
                }
                else
                {
                    LaunchApp(_draggedAppItem, false);
                }

                _draggedAppItem = null;
                _sourceCategory = null;
                _sourceGrid = null;
                _isAppDragging = false;
            }
        }

        private (GridView? grid, AppCategory? category, int index) GetHoveredDropTarget(Windows.Foundation.Point pointerPos)
        {
            foreach (var grid in GetAllCategoryGrids())
            {
                var transform = grid.TransformToVisual(MenuContainer);
                var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, grid.ActualWidth, grid.ActualHeight));

                bounds.X -= 10; bounds.Y -= 10; bounds.Width += 20; bounds.Height += 40;

                if (bounds.Contains(pointerPos))
                {
                    int index = grid.Items.Count;
                    double closestDist = double.MaxValue;

                    for (int i = 0; i < grid.Items.Count; i++)
                    {
                        var item = grid.Items[i] as AppItem;
                        if (item == _placeholderItem) continue;

                        if (grid.ContainerFromIndex(i) is FrameworkElement itemContainer)
                        {
                            var itemTransform = itemContainer.TransformToVisual(MenuContainer);
                            var itemBounds = itemTransform.TransformBounds(new Windows.Foundation.Rect(0, 0, itemContainer.ActualWidth, itemContainer.ActualHeight));

                            var centerX = itemBounds.X + (itemBounds.Width / 2);
                            var centerY = itemBounds.Y + (itemBounds.Height / 2);

                            double dist = Math.Pow(pointerPos.X - centerX, 2) + Math.Pow(pointerPos.Y - centerY, 2);
                            if (dist < closestDist)
                            {
                                closestDist = dist;
                                index = pointerPos.X < centerX ? i : i + 1;
                            }
                        }
                    }

                    var cat = grid.DataContext as AppCategory;
                    if (cat == null)
                    {
                        if (grid.Name == "ProductivityAppsGrid" && PinnedCategories.Count > 0) cat = PinnedCategories[0];
                        else if (grid.Name == "SecondaryAppsGrid" && PinnedCategories.Count > 1) cat = PinnedCategories[1];
                    }

                    return (grid, cat, index);
                }
            }
            return (null, null, -1);
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
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(120, 200, 200, 200))
            };

            if (_draggedAppItem.HasIcon == Visibility.Visible)
            {
                var image = new Image
                {
                    Source = _draggedAppItem.IconSource,
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform
                    {
                        ScaleX = _draggedAppItem.IconScale,
                        ScaleY = _draggedAppItem.IconScale
                    }
                };

                panel.Children.Add(image);
            }
            else
            {
                panel.Children.Add(new FontIcon
                {
                    Glyph = _draggedAppItem.FallbackGlyph,
                    FontSize = 32,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
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

        private IEnumerable<GridView> GetAllCategoryGrids()
        {
            var grids = new List<GridView>();

            if (PagesFlipView != null)
            {
                for (int i = 0; i < Pages.Count; i++)
                {
                    var container = PagesFlipView.ContainerFromIndex(i) as FrameworkElement;
                    if (container != null)
                    {
                        FindAllGridViewsRecursive(container, grids);
                    }
                }
            }

            if (ProductivityAppsGrid != null) grids.Add(ProductivityAppsGrid);
            if (SecondaryAppsGrid != null) grids.Add(SecondaryAppsGrid);

            return grids.Distinct();
        }

        private void FindAllGridViewsRecursive(DependencyObject parent, List<GridView> results)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is GridView gv)
                {
                    results.Add(gv);
                }
                FindAllGridViewsRecursive(child, results);
            }
        }

        private T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            var parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is T t) return t;
            return FindVisualParent<T>(parent);
        }
        #endregion

        #region Search & Action Handlers

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                PerformSearch(sender.Text);
            }
        }

        private void SearchFilter_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBox1 == null || SearchBox2 == null || DesignSplitStandard == null) return;

            if (sender is MenuFlyoutItem item && item.Tag is string tag)
            {
                _currentSearchFilter = tag;

                string query = DesignSplitStandard.Visibility == Visibility.Visible ? SearchBox1.Text : SearchBox2.Text;
                PerformSearch(query);
            }
        }

        private void PerformSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                if (_isShowingAllApps)
                {
                    if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.ItemsSource = AllAppsCollection;
                }
                else
                {
                    if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.Visibility = Visibility.Collapsed;
                    if (PagesFlipView != null) PagesFlipView.Visibility = Visibility.Visible;
                    if (BottomNavigationGrid != null) BottomNavigationGrid.Visibility = Visibility.Visible;
                }

                if (DefaultRightPane1 != null) DefaultRightPane1.Visibility = Visibility.Visible;
                if (SearchRightPane1 != null) SearchRightPane1.Visibility = Visibility.Collapsed;
                return;
            }

            ViewModel.PerformSearch(query, _currentSearchFilter, DispatcherQueue, (fileItem) =>
            {
                if (SearchResultsCollection.Count == 2)
                {
                    if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.SelectedIndex = 0;
                    if (ProductivityAppsGrid != null) ProductivityAppsGrid.SelectedIndex = 0;
                    if (SecondaryAppsGrid != null) SecondaryAppsGrid.SelectedIndex = 0;
                    UpdateSearchDetailsPane(fileItem);
                }
            });

            if (SearchAndAllAppsGrid != null)
            {
                SearchAndAllAppsGrid.Visibility = Visibility.Visible;
                SearchAndAllAppsGrid.ItemsSource = SearchResultsCollection;
            }

            if (ProductivityAppsGrid != null) ProductivityAppsGrid.ItemsSource = SearchResultsCollection;
            if (SecondaryAppsGrid != null) SecondaryAppsGrid.ItemsSource = SearchResultsCollection;

            if (DefaultRightPane1 != null) DefaultRightPane1.Visibility = Visibility.Collapsed;
            if (DefaultRightPane2 != null) DefaultRightPane2.Visibility = Visibility.Collapsed;
            if (SearchRightPane1 != null) SearchRightPane1.Visibility = Visibility.Visible;
            if (SearchRightPane2 != null) SearchRightPane2.Visibility = Visibility.Visible;

            if (SearchResultsCollection.Count > 0)
            {
                if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.SelectedIndex = 0;
                if (ProductivityAppsGrid != null) ProductivityAppsGrid.SelectedIndex = 0;
                if (SecondaryAppsGrid != null) SecondaryAppsGrid.SelectedIndex = 0;
                UpdateSearchDetailsPane(SearchResultsCollection.First());
            }
            else
            {
                _currentSearchItem = null;
                string noResultsTxt = LocalizationService.Instance.GetString("StartMenu_SearchNoResults");
                if (SearchDetailsName1 != null) SearchDetailsName1.Text = noResultsTxt;
                if (SearchDetailsName2 != null) SearchDetailsName2.Text = noResultsTxt;
                if (SearchDetailsIcon1 != null) SearchDetailsIcon1.Source = null;
                if (SearchDetailsIcon2 != null) SearchDetailsIcon2.Source = null;
                if (AdminBtn1 != null) AdminBtn1.Visibility = Visibility.Collapsed;
                if (LocationBtn1 != null) LocationBtn1.Visibility = Visibility.Collapsed;
                if (AdminBtn2 != null) AdminBtn2.Visibility = Visibility.Collapsed;
                if (LocationBtn2 != null) LocationBtn2.Visibility = Visibility.Collapsed;
            }
        }

        private void AppGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is AppItem item && SearchRightPane1 != null && SearchRightPane1.Visibility == Visibility.Visible)
            {
                UpdateSearchDetailsPane(item);
            }
        }

        private void UpdateSearchDetailsPane(AppItem item)
        {
            _currentSearchItem = item;
            if (SearchDetailsName1 != null) SearchDetailsName1.Text = item.Name;
            if (SearchDetailsName2 != null) SearchDetailsName2.Text = item.Name;
            if (SearchDetailsIcon1 != null) SearchDetailsIcon1.Source = item.IconSource;
            if (SearchDetailsIcon2 != null) SearchDetailsIcon2.Source = item.IconSource;

            bool isSpecial = item.ExecutablePath?.StartsWith("WEB_SEARCH:") == true || item.ExecutablePath?.StartsWith("FILE_SEARCH:") == true;
            bool isUwpApp = item.IsUwp;

            if (AdminBtn1 != null) AdminBtn1.Visibility = (!isUwpApp && !isSpecial) ? Visibility.Visible : Visibility.Collapsed;
            if (LocationBtn1 != null) LocationBtn1.Visibility = (!isSpecial) ? Visibility.Visible : Visibility.Collapsed;

            if (AdminBtn2 != null) AdminBtn2.Visibility = (!isUwpApp && !isSpecial) ? Visibility.Visible : Visibility.Collapsed;
            if (LocationBtn2 != null) LocationBtn2.Visibility = (!isSpecial) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AppGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app)
            {
                if (app.IsFolderItem == Visibility.Visible)
                {
                    app.IsExpanded = !app.IsExpanded;

                    if (app.FolderApps.Count == 0 && !string.IsNullOrEmpty(app.ExecutablePath))
                    {
                        try
                        {
                            var files = Directory.GetFiles(app.ExecutablePath, "*.lnk", SearchOption.AllDirectories)
                                .Concat(Directory.GetFiles(app.ExecutablePath, "*.url", SearchOption.AllDirectories))
                                .Concat(Directory.GetFiles(app.ExecutablePath, "*.appref-ms", SearchOption.AllDirectories));

                            foreach (var file in files)
                            {
                                app.FolderApps.Add(new AppItem
                                {
                                    Name = Path.GetFileNameWithoutExtension(file),
                                    ExecutablePath = file,
                                    IsUwp = false,
                                    FallbackGlyph = "\xE738",
                                    IconScale = 1.0
                                });
                            }

                            _ = ViewModel.ExtractIconsAsync(app.FolderApps);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to load folder contents: {ex.Message}");
                        }
                    }

                    var container = SearchAndAllAppsGrid.ContainerFromItem(app) as ListViewItem;

                    if (container != null)
                    {
                        var chevron = FindDescendant<FontIcon>(container, "ChevronIcon");
                        if (chevron != null && chevron.RenderTransform is RotateTransform transform)
                        {
                            FactoryAnimation.AnimateRotation(transform, app.IsExpanded ? 180 : 0);
                        }

                        var nestedFolderGrid = FindDescendant<ItemsControl>(container, "NestedFolderGrid");
                        if (nestedFolderGrid != null)
                        {
                            FactoryAnimation.AnimatePanelExpansion(nestedFolderGrid, app.IsExpanded, 0);

                            if (app.IsExpanded)
                            {
                                DispatcherQueue.TryEnqueue(async () =>
                                {
                                    await Task.Delay(260);

                                    container.StartBringIntoView(new BringIntoViewOptions
                                    {
                                        AnimationDesired = true
                                    });
                                });
                            }
                        }
                    }
                }
                else
                {
                    LaunchApp(app, false);
                }
            }
        }

        private T? FindDescendant<T>(DependencyObject obj, string name) where T : FrameworkElement
        {
            if (obj == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);

                if (child is T element && element.Name == name)
                {
                    return element;
                }

                var result = FindDescendant<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void SearchAction_Open_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSearchItem != null) LaunchApp(_currentSearchItem, false);
        }

        private void SearchAction_RunAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSearchItem != null) LaunchApp(_currentSearchItem, true);
        }

        private void SearchAction_OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSearchItem == null || string.IsNullOrEmpty(_currentSearchItem.ExecutablePath)) return;
            try
            {
                string? dir = Path.GetDirectoryName(_currentSearchItem.ExecutablePath);
                if (!string.IsNullOrEmpty(dir))
                    Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
            HideMenu();
        }

        private async void LaunchApp(AppItem app, bool runAsAdmin)
        {
            if (string.IsNullOrEmpty(app.ExecutablePath)) return;
            try
            {
                if (app.ExecutablePath.StartsWith("WEB_SEARCH:"))
                {
                    string query = app.ExecutablePath.Substring(11);
                    await Launcher.LaunchUriAsync(new Uri($"https://www.google.com/search?q={Uri.EscapeDataString(query)}"));
                    HideMenu();
                    return;
                }
                else if (app.ExecutablePath.StartsWith("FILE_SEARCH:"))
                {
                    string query = app.ExecutablePath.Substring(12);
                    await Launcher.LaunchUriAsync(new Uri($"search-ms:query={Uri.EscapeDataString(query)}"));
                    HideMenu();
                    return;
                }

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
                HideMenu();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }
        #endregion
    }
}