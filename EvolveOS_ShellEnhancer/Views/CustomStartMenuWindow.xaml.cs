// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System.Collections.ObjectModel;
using System.IO;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

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
        public ObservableCollection<AppItem> SearchAppsCollection => ViewModel.SearchAppsCollection;
        public ObservableCollection<AppItem> SearchSettingsCollection => ViewModel.SearchSettingsCollection;
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
        private bool _isSearchAppsExpanded = false;
        private bool _isSearchSettingsExpanded = false;

        private AppItem? _sourceFolderItem;

        private int _pageNameAnimationToken = 0;

        public ObservableCollection<FolderDot> FolderDots { get; } = new();
        private int _currentFolderPage = 0;
        private readonly int _folderItemsPerPage = 12;
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

            if (this.Content is UIElement rootElement)
            {
                rootElement.CharacterReceived += RootGrid_CharacterReceived;
            }

            ViewModel.OnUserProfileLoaded = (name, pic, email, accountType) =>
            {
                _currentUserEmail = email;
                _currentAccountType = accountType;

                if (UnifiedProfileName != null) UnifiedProfileName.Text = name;
                if (UnifiedProfileName2 != null) UnifiedProfileName2.Text = name;

                if (UnifiedProfilePic != null)
                {
                    UnifiedProfilePic.DisplayName = name;
                    if (pic != null) UnifiedProfilePic.ProfilePicture = pic;
                }
                if (UnifiedProfilePic2 != null)
                {
                    UnifiedProfilePic2.DisplayName = name;
                    if (pic != null) UnifiedProfilePic2.ProfilePicture = pic;
                }

                bool enableProfile = SettingsEngine.Shell_StartMenuProfileClick;
                if (ProfileButton != null)
                {
                    ProfileButton.IsHitTestVisible = enableProfile;
                    ProfileButton.IsEnabled = enableProfile;
                }
                if (ProfileButton2 != null)
                {
                    ProfileButton2.IsHitTestVisible = enableProfile;
                    ProfileButton2.IsEnabled = enableProfile;
                }
            };

            ViewModel.OnAppsDataLoaded = () =>
            {
                string savedNames = SettingsEngine.StartMenuPageNames ?? string.Empty;
                var namesArray = savedNames.Split('|');

                for (int i = 0; i < Math.Min(ViewModel.Pages.Count, namesArray.Length); i++)
                {
                    if (!string.IsNullOrWhiteSpace(namesArray[i]))
                    {
                        ViewModel.Pages[i].PageName = namesArray[i];
                    }
                }

                UpdatePageIndicators(0);
                int savedSize = SettingsEngine.Shell_StartMenuFolderSize;
                if (savedSize < 1 || savedSize > 2) savedSize = 1;

                foreach (var page in ViewModel.Pages)
                {
                    foreach (var category in page.PinnedCategories)
                    {
                        if (category.Tabs != null && category.Tabs.Count > 0)
                        {
                            category.IsTabbed = true;

                            foreach (var tab in category.Tabs)
                            {
                                foreach (var app in tab.Apps)
                                {
                                    if (app.ExecutablePath == "PINNED_FOLDER")
                                    {
                                        app.FolderSize = savedSize;
                                    }
                                }
                            }

                            category.SelectedTabIndex = 0;
                        }
                        else
                        {
                            foreach (var app in category.Apps)
                            {
                                if (app.ExecutablePath == "PINNED_FOLDER")
                                {
                                    app.FolderSize = savedSize;
                                }
                            }
                        }
                    }
                }

                foreach (var grid in GetAllCategoryGrids())
                {
                    if (grid.ItemsPanelRoot is StartMenuWrapPanel wrapPanel)
                    {
                        wrapPanel.InvalidateMeasure();
                        wrapPanel.InvalidateArrange();
                    }
                }
            };

            ViewModel.InitializeAppWatchers(DispatcherQueue);
            ViewModel.LoadAppsData(DispatcherQueue);
            ViewModel.LoadUserProfile(DispatcherQueue);
            ViewModel.UpdateShortcuts(SettingsEngine.Shell_StartMenuShortcuts ?? string.Empty);
        }

        private void RootGrid_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            var focusedElement = FocusManager.GetFocusedElement(this.Content.XamlRoot);
            if (focusedElement is TextBox || focusedElement is AutoSuggestBox) return;

            if ((_currentStyle == "Compact" || _currentStyle == "SplitGrouped") && SearchOverlay2 != null && SearchOverlay2.Visibility == Visibility.Collapsed)
            {
                if (!char.IsControl(args.Character))
                {
                    if (MainSplitContentGrid != null) MainSplitContentGrid.Visibility = Visibility.Collapsed;
                    SearchOverlay2.Visibility = Visibility.Visible;
                    SearchBox2.Text = args.Character.ToString();
                    PerformSearch(SearchBox2.Text);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SearchBox2.Focus(FocusState.Programmatic);
                        SearchBox2.Text = SearchBox2.Text;
                    });

                    args.Handled = true;
                }
            }
        }

        public void ReloadTheme()
        {
            string savedTheme = SettingsEngine.Shell_AppTheme ?? "Default";
            SetTheme(savedTheme);
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

            if (SearchBox1 != null) SearchBox1.Text = string.Empty;
            if (SearchBox2 != null) SearchBox2.Text = string.Empty;

            if (DesignSplitStandard != null) DesignSplitStandard.Visibility = isStandard ? Visibility.Visible : Visibility.Collapsed;
            if (DesignSplitGrouped != null) DesignSplitGrouped.Visibility = isGrouped ? Visibility.Visible : Visibility.Collapsed;

            if (isStandard && PagesFlipView != null) HideAllFlipViewButtons(PagesFlipView);
            if (isGrouped && PagesFlipView2 != null) HideAllFlipViewButtons(PagesFlipView2);

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

            bool isGroupedStyle = (_currentStyle == "Compact" || _currentStyle == "SplitGrouped");

            if (isGroupedStyle && PagesFlipView2 != null) HideAllFlipViewButtons(PagesFlipView2);
            else if (!isGroupedStyle && PagesFlipView != null) HideAllFlipViewButtons(PagesFlipView);

            if (SearchBox2 != null) SearchBox2.Text = string.Empty;
            if (SearchOverlay2 != null) SearchOverlay2.Visibility = Visibility.Collapsed;
            if (MainSplitContentGrid != null) MainSplitContentGrid.Visibility = Visibility.Visible;

            int windowWidth = isGroupedStyle ? 920 : 780;
            int windowHeight = isGroupedStyle ? 720 : 680;

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

            bool isStandardStyle = (_currentStyle == "Standard" || _currentStyle == "SplitStandard");

            if (EnableAnimations)
            {
                int startX = x, startY = y;
                if (targetPos == "Top") startY = y - windowHeight - 15;
                else if (targetPos == "Left") startX = x - windowWidth - 15;
                else if (targetPos == "Right") startX = x + windowWidth + 15;
                else startY = y + windowHeight + 15;

                _appWindow.MoveAndResize(new RectInt32(startX, startY, windowWidth, windowHeight));
                _appWindow.Show();

                this.Activate();
                Win32Helper.SetForegroundWindow(_hWnd);

                if (AvatarPopup != null) AvatarPopup.IsOpen = isStandardStyle;

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
                _appWindow.MoveAndResize(new RectInt32(x, y, windowWidth, windowHeight));
                _appWindow.Show();

                this.Activate();
                Win32Helper.SetForegroundWindow(_hWnd);

                if (AvatarPopup != null) AvatarPopup.IsOpen = isStandardStyle;

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

            if (SearchBox1 != null) SearchBox1.Text = string.Empty;
            if (SearchBox2 != null) SearchBox2.Text = string.Empty;

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
                        if (AvatarPopup != null) AvatarPopup.IsOpen = false;

                        _activeAccountCardWindow?.Close();
                        _activeAccountCardWindow = null;
                    });
            }
            else
            {
                _appWindow.Hide();
                if (AvatarPopup != null) AvatarPopup.IsOpen = false;

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
                int offsetX;
                int offsetY = _appWindow.Position.Y + 60;

                if (sender is Button clickedButton && clickedButton == ProfileButton2)
                {
                    offsetX = _appWindow.Position.X + 24;
                }
                else
                {
                    offsetX = _appWindow.Position.X + _appWindow.Size.Width - cardWidth - 16;
                }

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
            if (SettingsEngine.Shell_StartMenuProfileClick && ProfileGrowStoryboard != null) ProfileGrowStoryboard.Begin();
        }

        private void ProfileButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (SettingsEngine.Shell_StartMenuProfileClick && ProfileShrinkStoryboard != null) ProfileShrinkStoryboard.Begin();
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
                        new RunWindow().Activate();
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
            PinnedCategories.Add(new AppCategory { Name = LocalizationService.Instance.GetString("StartMenu_NewGroup") ?? "New Group" });
            SafeSavePins();
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem btn && btn.Tag is AppCategory cat)
            {
                var page = Pages.FirstOrDefault(p => p.PinnedCategories.Contains(cat));
                if (page != null)
                {
                    page.PinnedCategories.Remove(cat);
                    SafeSavePins();
                }
            }
        }

        private void MoveGroupUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem btn && (btn.Tag as AppCategory ?? btn.DataContext as AppCategory) is AppCategory cat)
            {
                var page = Pages.FirstOrDefault(p => p.PinnedCategories.Contains(cat));
                if (page != null)
                {
                    int currentIndex = page.PinnedCategories.IndexOf(cat);
                    if (currentIndex > 0)
                    {
                        page.PinnedCategories.Move(currentIndex, currentIndex - 1);
                        SafeSavePins();
                    }
                }
            }
        }

        private void MoveGroupDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem btn && (btn.Tag as AppCategory ?? btn.DataContext as AppCategory) is AppCategory cat)
            {
                var page = Pages.FirstOrDefault(p => p.PinnedCategories.Contains(cat));
                if (page != null)
                {
                    int currentIndex = page.PinnedCategories.IndexOf(cat);
                    if (currentIndex >= 0 && currentIndex < page.PinnedCategories.Count - 1)
                    {
                        page.PinnedCategories.Move(currentIndex, currentIndex + 1);
                        SafeSavePins();
                    }
                }
            }
        }

        private void AddTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppCategory parentCategory)
            {
                parentCategory.Tabs.Add(new AppCategory
                {
                    Name = LocalizationService.Instance.GetString("StartMenu_NewTab") ?? "New Tab"
                });

                parentCategory.SelectedTabIndex = parentCategory.Tabs.Count - 1;

                SafeSavePins();
            }
        }

        private async void RenameTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is AppCategory tabCategory)
            {
                var dialog = new ContentDialog
                {
                    Title = LocalizationService.Instance.GetString("StartMenu_RenameTab") ?? "Rename Tab",
                    PrimaryButtonText = LocalizationService.Instance.GetString("StartMenu_Save") ?? "Save",
                    CloseButtonText = LocalizationService.Instance.GetString("StartMenu_Cancel") ?? "Cancel",
                    XamlRoot = this.Content.XamlRoot,
                    RequestedTheme = this.Content is FrameworkElement fe ? fe.RequestedTheme : ElementTheme.Default
                };

                var nameBox = new TextBox
                {
                    Text = tabCategory.Name,
                    Width = 300
                };
                dialog.Content = nameBox;

                if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    tabCategory.Name = nameBox.Text;
                    SafeSavePins();
                }
            }
        }

        private void DeleteTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is AppCategory tabCategory)
            {
                foreach (var page in Pages)
                {
                    foreach (var parentCategory in page.PinnedCategories)
                    {
                        if (parentCategory.Tabs != null && parentCategory.Tabs.Contains(tabCategory))
                        {
                            parentCategory.Tabs.Remove(tabCategory);

                            if (parentCategory.SelectedTabIndex >= parentCategory.Tabs.Count)
                            {
                                parentCategory.SelectedTabIndex = Math.Max(0, parentCategory.Tabs.Count - 1);
                            }

                            SafeSavePins();
                            return;
                        }
                    }
                }
            }
        }

        private void CategoryName_LostFocus(object sender, RoutedEventArgs e)
        {
            SafeSavePins();
        }

        private void CategoryName_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                this.Content.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        private void PagesFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is FlipView fv && fv.SelectedIndex >= 0)
            {
                UpdatePageIndicators(fv.SelectedIndex);

                if (fv == PagesFlipView && PagesFlipView2 != null && PagesFlipView2.SelectedIndex != fv.SelectedIndex)
                {
                    PagesFlipView2.SelectedIndex = fv.SelectedIndex;
                }
                else if (fv == PagesFlipView2 && PagesFlipView != null && PagesFlipView.SelectedIndex != fv.SelectedIndex)
                {
                    PagesFlipView.SelectedIndex = fv.SelectedIndex;
                }

                if (_isVisible && fv.SelectedIndex >= 0 && fv.SelectedIndex < Pages.Count)
                {
                    var page = Pages[fv.SelectedIndex];
                    string pageName = string.IsNullOrWhiteSpace(page.PageName) ? $"Page {page.PageIndex + 1}" : page.PageName;
                    ShowPageNameBriefly(pageName);
                }
            }
        }

        private void BottomNavigationGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
            var activeFlipView = (_currentStyle == "Compact" || _currentStyle == "SplitGrouped") ? PagesFlipView2 : PagesFlipView;

            if (activeFlipView != null)
            {
                if (delta < 0 && activeFlipView.SelectedIndex < Pages.Count - 1)
                {
                    activeFlipView.SelectedIndex++;
                }
                else if (delta > 0 && activeFlipView.SelectedIndex > 0)
                {
                    activeFlipView.SelectedIndex--;
                }

                e.Handled = true;
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
            var activeFlipView = (_currentStyle == "Compact" || _currentStyle == "SplitGrouped") ? PagesFlipView2 : PagesFlipView;
            if (activeFlipView != null && activeFlipView.SelectedIndex > 0)
            {
                activeFlipView.SelectedIndex -= 1;
            }
        }

        private void PageRightBtn_Click(object sender, RoutedEventArgs e)
        {
            var activeFlipView = (_currentStyle == "Compact" || _currentStyle == "SplitGrouped") ? PagesFlipView2 : PagesFlipView;
            if (activeFlipView != null && activeFlipView.SelectedIndex >= 0 && activeFlipView.SelectedIndex < Pages.Count - 1)
            {
                activeFlipView.SelectedIndex += 1;
            }
        }

        private void PageDotIndicator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int targetIndex)
            {
                var activeFlipView = (_currentStyle == "Compact" || _currentStyle == "SplitGrouped") ? PagesFlipView2 : PagesFlipView;
                if (activeFlipView != null && targetIndex >= 0 && targetIndex < Pages.Count)
                {
                    activeFlipView.SelectedIndex = targetIndex;
                }
            }
        }

        private void PagesFlipView_Loaded(object sender, RoutedEventArgs e)
        {
            PagesFlipView.ApplyTemplate();
            PagesFlipView2.ApplyTemplate();
            HideAllFlipViewButtons(PagesFlipView);
            HideAllFlipViewButtons(PagesFlipView2);
        }

        private void PagesFlipView_LayoutUpdated(object sender, object e)
        {
            HideAllFlipViewButtons(PagesFlipView);
            HideAllFlipViewButtons(PagesFlipView2);
        }

        private void PagesFlipView_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            HideAllFlipViewButtons(PagesFlipView);
            HideAllFlipViewButtons(PagesFlipView2);
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

        private void CategoryGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is GridView gridView) EvaluateChevronVisibility(gridView, e.NewSize.Width);
        }

        private void CategoryChevron_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn)
            {
                if (btn.Content is FontIcon icon && icon.RenderTransform is RotateTransform transform)
                    FactoryAnimation.AnimateRotation(transform, 180);

                if (btn.Parent is Grid headerGrid && headerGrid.Parent is StackPanel panel && panel.Children[1] is GridView gridView)
                {
                    gridView.ClearValue(FrameworkElement.MaxHeightProperty);

                    double[] heights = gridView.Tag as double[] ?? new double[] { 104.0, 500.0 };
                    double fullHeight = heights[1];

                    if (gridView.ItemsPanelRoot is Panel panelRoot)
                    {
                        panelRoot.Measure(new Size(gridView.ActualWidth, 10000.0));
                        if (panelRoot.DesiredSize.Height > fullHeight) fullHeight = panelRoot.DesiredSize.Height;
                    }

                    FactoryAnimation.AnimatePanelExpansion(gridView, true, fullHeight);

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(350);
                        if (btn.IsChecked == true) gridView.ClearValue(FrameworkElement.HeightProperty);
                    });
                }
            }
        }

        private void CategoryChevron_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn)
            {
                if (btn.Content is FontIcon icon && icon.RenderTransform is RotateTransform transform)
                    FactoryAnimation.AnimateRotation(transform, 0);

                if (btn.Parent is Grid headerGrid && headerGrid.Parent is StackPanel panel && panel.Children[1] is GridView gridView)
                {
                    double[] heights = gridView.Tag as double[] ?? new double[] { 104.0, 500.0 };
                    double baseHeight = heights[0];

                    gridView.MaxHeight = baseHeight;
                    FactoryAnimation.AnimatePanelExpansion(gridView, false, baseHeight);
                }
            }
        }

        private void FadeElement(UIElement target, double to, int durationMs)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, "Opacity");
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private async void ShowPageNameBriefly(string pageName)
        {
            if (PageNameDisplay1 != null) PageNameDisplay1.Text = pageName;
            if (PageNameDisplay2 != null) PageNameDisplay2.Text = pageName;

            int token = ++_pageNameAnimationToken;

            if (PageNameDisplay1 != null) FadeElement(PageNameDisplay1, 1.0, 300);
            if (PageNameDisplay2 != null) FadeElement(PageNameDisplay2, 1.0, 300);

            await Task.Delay(1500);

            if (token == _pageNameAnimationToken)
            {
                if (PageNameDisplay1 != null) FadeElement(PageNameDisplay1, 0.0, 500);
                if (PageNameDisplay2 != null) FadeElement(PageNameDisplay2, 0.0, 500);
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

                var renameItem = new MenuFlyoutItem { Text = LocalizationService.Instance.GetString("StartMenu_Rename") ?? "Rename", Icon = new FontIcon { Glyph = "\xE8AC" } };
                renameItem.Click += async (s, args) => { await RenamePageAsync(currentPage); };
                flyout.Items.Add(renameItem);

                flyout.Items.Add(new MenuFlyoutSeparator());

                var newItem = new MenuFlyoutSubItem { Text = LocalizationService.Instance.GetString("StartMenu_New") ?? "New", Icon = new FontIcon { Glyph = "\xE710" } };

                var addGroupItem = new MenuFlyoutItem { Text = LocalizationService.Instance.GetString("StartMenu_AddGroup") ?? "Add Group", Icon = new FontIcon { Glyph = "\xE8F4" } };
                addGroupItem.Click += (s, args) =>
                {
                    currentPage.PinnedCategories.Add(new AppCategory { Name = LocalizationService.Instance.GetString("StartMenu_NewGroup") ?? "New Group" });
                    SafeSavePins();
                };
                newItem.Items.Add(addGroupItem);

                var addTabbedGroupItem = new MenuFlyoutItem { Text = LocalizationService.Instance.GetString("StartMenu_AddTabbedGroup") ?? "Add Tabbed Group", Icon = new FontIcon { Glyph = "\xE8D2" } };
                addTabbedGroupItem.Click += (s, args) =>
                {
                    var tabCat = new AppCategory { Name = LocalizationService.Instance.GetString("StartMenu_NewTabbedGroup") ?? "New Tabbed Group" };

                    tabCat.IsTabbed = true;

                    tabCat.Tabs.Add(new AppCategory { Name = LocalizationService.Instance.GetString("StartMenu_NewTab") ?? "Tab 1" });

                    currentPage.PinnedCategories.Add(tabCat);
                    SafeSavePins();
                };
                newItem.Items.Add(addTabbedGroupItem);

                var addPageItem = new MenuFlyoutItem { Text = LocalizationService.Instance.GetString("StartMenu_AddPage") ?? "Add Page", Icon = new FontIcon { Glyph = "\xE7C3" } };
                addPageItem.Click += (s, args) =>
                {
                    var newPage = new StartMenuPage { PageIndex = Pages.Count };
                    newPage.PageName = LocalizationService.Instance.GetString("StartMenu_NewPage") ?? "New Page";
                    var prop = typeof(StartMenuPage).GetProperty("PageName");
                    if (prop != null) prop.SetValue(newPage, LocalizationService.Instance.GetString("StartMenu_NewPage") ?? "New Page");

                    newPage.PinnedCategories.Add(new AppCategory { Name = LocalizationService.Instance.GetString("StartMenu_NewGroup") ?? "New Group" });
                    Pages.Add(newPage);

                    if (PagesFlipView != null) PagesFlipView.SelectedIndex = Pages.Count - 1;
                    UpdatePageIndicators(Pages.Count - 1);
                    SafeSavePins();
                };
                newItem.Items.Add(addPageItem);

                newItem.Items.Add(new MenuFlyoutSeparator());

                var pinFileItem = new MenuFlyoutItem { Text = LocalizationService.Instance.GetString("StartMenu_PinFile") ?? "Pin File", Icon = new FontIcon { Glyph = "\xE8E5" } };
                pinFileItem.Click += async (s, args) => { await PinFileAsync(currentPage); };
                newItem.Items.Add(pinFileItem);

                var pinFolderItem = new MenuFlyoutItem { Text = LocalizationService.Instance.GetString("StartMenu_PinFolder") ?? "Pin Folder", Icon = new FontIcon { Glyph = "\xE8D5" } };
                pinFolderItem.Click += async (s, args) => { await PinFolderAsync(currentPage); };
                newItem.Items.Add(pinFolderItem);

                var pinWebItem = new MenuFlyoutItem { Text = LocalizationService.Instance.GetString("StartMenu_PinWebSite") ?? "Pin WebSite", Icon = new FontIcon { Glyph = "\xE12B" } };
                pinWebItem.Click += async (s, args) => { await PinWebSiteAsync(currentPage); };
                newItem.Items.Add(pinWebItem);

                flyout.Items.Add(newItem);

                var removeItem = new MenuFlyoutItem
                {
                    Text = LocalizationService.Instance.GetString("StartMenu_Remove") ?? "Remove",
                    Icon = new FontIcon { Glyph = "\xE74D" },
                    IsEnabled = Pages.IndexOf(currentPage) > 0
                };

                removeItem.Click += (s, args) =>
                {
                    if (Pages.Count > 1 && Pages.IndexOf(currentPage) > 0)
                    {
                        Pages.Remove(currentPage);

                        for (int i = 0; i < Pages.Count; i++) Pages[i].PageIndex = i;

                        if (PagesFlipView != null) PagesFlipView.SelectedIndex = Math.Max(0, Pages.Count - 1);
                        UpdatePageIndicators(Math.Max(0, Pages.Count - 1));

                        SafeSavePins();
                    }
                };
                flyout.Items.Add(removeItem);

                flyout.ShowAt(element, e.GetPosition(element));
                e.Handled = true;
            }
        }

        private async Task RenamePageAsync(StartMenuPage page)
        {
            var dialog = new ContentDialog
            {
                Title = LocalizationService.Instance.GetString("StartMenu_RenamePage") ?? "Rename Page",
                PrimaryButtonText = LocalizationService.Instance.GetString("StartMenu_Save") ?? "Save",
                CloseButtonText = LocalizationService.Instance.GetString("StartMenu_Cancel") ?? "Cancel",
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = this.Content is FrameworkElement fe ? fe.RequestedTheme : ElementTheme.Default
            };

            var nameBox = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(page.PageName) ? $"Page {page.PageIndex + 1}" : page.PageName,
                Width = 300
            };
            dialog.Content = nameBox;

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
            {
                page.PageName = nameBox.Text;
                SafeSavePins();

                ShowPageNameBriefly(page.PageName);
            }
        }

        private async Task PinFileAsync(StartMenuPage page)
        {
            string title = LocalizationService.Instance.GetString("StartMenu_PinFile") ?? "Pin File";

            string? filePath = Win32FileDialogHelper.ShowOpenFilePicker(
                this,
                title,
                "All Files",
                "*.*");

            if (!string.IsNullOrEmpty(filePath))
            {
                var appItem = new AppItem
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    ExecutablePath = filePath,
                    FallbackGlyph = "\xE8E5",
                    IsUwp = false,
                    FolderSize = 1,
                    IconScale = 1.0
                };

                _ = ViewModel.ExtractIconsAsync(new[] { appItem });

                var targetCategory = page.PinnedCategories.FirstOrDefault() ?? new AppCategory { Name = LocalizationService.Instance.GetString("StartMenu_PinnedCategory") ?? "Pinned" };
                if (!page.PinnedCategories.Contains(targetCategory)) page.PinnedCategories.Add(targetCategory);

                targetCategory = GetEffectiveTargetCategory(targetCategory);
                targetCategory?.Apps.Add(appItem);
                SafeSavePins();
            }

            await Task.CompletedTask;
        }

        private async Task PinFolderAsync(StartMenuPage page)
        {
            string title = LocalizationService.Instance.GetString("StartMenu_PinFolder") ?? "Pin Folder";

            string? folderPath = Win32FileDialogHelper.ShowFolderPicker(this, title);

            if (!string.IsNullOrEmpty(folderPath))
            {
                var appItem = new AppItem
                {
                    Name = Path.GetFileName(folderPath),
                    ExecutablePath = folderPath,
                    FallbackGlyph = "\xE8D5",
                    IsUwp = false,
                    FolderSize = 1,
                    IconScale = 1.0
                };

                var targetCategory = page.PinnedCategories.FirstOrDefault() ?? new AppCategory { Name = LocalizationService.Instance.GetString("StartMenu_PinnedCategory") ?? "Pinned" };
                if (!page.PinnedCategories.Contains(targetCategory)) page.PinnedCategories.Add(targetCategory);

                targetCategory = GetEffectiveTargetCategory(targetCategory);
                targetCategory?.Apps.Add(appItem);
                SafeSavePins();
            }

            await Task.CompletedTask;
        }

        private async Task PinWebSiteAsync(StartMenuPage page)
        {
            var dialog = new ContentDialog
            {
                Title = LocalizationService.Instance.GetString("StartMenu_PinWebSite") ?? "Pin WebSite",
                PrimaryButtonText = LocalizationService.Instance.GetString("StartMenu_Save") ?? "Save",
                CloseButtonText = LocalizationService.Instance.GetString("StartMenu_Cancel") ?? "Cancel",
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = this.Content is FrameworkElement fe ? fe.RequestedTheme : ElementTheme.Default
            };

            var stack = new StackPanel { Spacing = 12 };
            var nameBox = new TextBox { PlaceholderText = LocalizationService.Instance.GetString("StartMenu_WebsiteName") ?? "Website Name (e.g. Google)", Width = 300 };
            var urlBox = new TextBox { PlaceholderText = LocalizationService.Instance.GetString("StartMenu_WebsiteUrl") ?? "URL (e.g. https://google.com)", Width = 300 };
            stack.Children.Add(nameBox);
            stack.Children.Add(urlBox);
            dialog.Content = stack;

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(urlBox.Text))
            {
                string url = urlBox.Text.Trim();
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;

                var appItem = new AppItem
                {
                    Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Website" : nameBox.Text,
                    ExecutablePath = url,
                    FallbackGlyph = "\xE12B",
                    IsUwp = false,
                    FolderSize = 1,
                    IconScale = 1.0
                };

                var targetCategory = page.PinnedCategories.FirstOrDefault() ?? new AppCategory { Name = LocalizationService.Instance.GetString("StartMenu_PinnedCategory") ?? "Pinned" };
                if (!page.PinnedCategories.Contains(targetCategory)) page.PinnedCategories.Add(targetCategory);

                targetCategory = GetEffectiveTargetCategory(targetCategory);
                targetCategory?.Apps.Add(appItem);
                SafeSavePins();
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

                bool isPinnedToStart = GetAllCategoriesFlattened().Any(c => c.Apps.Contains(app));
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
                        foreach (var cat in GetAllCategoriesFlattened())
                        {
                            cat.Apps.Remove(app);
                        }
                    }
                    else
                    {
                        if (Pages.Count > 0 && Pages[0].PinnedCategories.Count > 0)
                        {
                            var targetCategory = GetEffectiveTargetCategory(Pages[0].PinnedCategories.First());
                            targetCategory?.Apps.Add(app);
                        }
                        else
                        {
                            if (Pages.Count == 0) Pages.Add(new StartMenuPage { PageIndex = 0 });
                            var defaultCat = new AppCategory { Name = LocalizationService.Instance.GetString("StartMenu_PinnedCategory") ?? "Pinned" };
                            defaultCat.Apps.Add(app);
                            Pages[0].PinnedCategories.Add(defaultCat);
                        }
                    }

                    SafeSavePins();
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

                flyout.ShowAt(element, e.GetPosition(element));
            }
        }

        private void RecentItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is FrameworkElement element && element.Tag is AppItem recentItem)
            {
                MenuFlyout flyout = new MenuFlyout();

                var adminItem = new MenuFlyoutItem
                {
                    Text = LocalizationService.Instance.GetString("StartMenu_ActionRunAsAdmin") ?? "Run as administrator",
                    Icon = new FontIcon { Glyph = "\xE7EF" }
                };
                adminItem.Click += (s, args) => LaunchApp(recentItem, true);
                flyout.Items.Add(adminItem);

                var locItem = new MenuFlyoutItem
                {
                    Text = LocalizationService.Instance.GetString("StartMenu_ActionOpenLocation") ?? "Open file location",
                    Icon = new FontIcon { Glyph = "\xE8DA" }
                };
                locItem.Click += (s, args) =>
                {
                    try
                    {
                        string? dir = Path.GetDirectoryName(recentItem.ExecutablePath);
                        if (!string.IsNullOrEmpty(dir))
                            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                    }
                    catch (Exception ex) { Debug.WriteLine(ex.Message); }
                    HideMenu();
                };
                flyout.Items.Add(locItem);

                flyout.Items.Add(new MenuFlyoutSeparator());

                var removeItem = new MenuFlyoutItem
                {
                    Text = LocalizationService.Instance.GetString("StartMenu_RemoveList") ?? "Remove from list",
                    Icon = new FontIcon { Glyph = "\xE711" }
                };

                removeItem.Click += (s, args) =>
                {
                    try
                    {
                        string recentFolder = Environment.GetFolderPath(Environment.SpecialFolder.Recent);

                        if (!string.IsNullOrEmpty(recentItem.ExecutablePath) &&
                            recentItem.ExecutablePath.StartsWith(recentFolder, StringComparison.OrdinalIgnoreCase) &&
                            recentItem.ExecutablePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                        {
                            if (File.Exists(recentItem.ExecutablePath))
                            {
                                File.Delete(recentItem.ExecutablePath);
                            }
                        }
                        else if (!string.IsNullOrEmpty(recentItem.ExecutablePath))
                        {
                            var recentLinks = Directory.GetFiles(recentFolder, "*.lnk");
                            foreach (var lnkPath in recentLinks)
                            {
                                string target = StartMenuHelper.ParseShortcutTarget(lnkPath);
                                if (!string.IsNullOrEmpty(target) && target.Equals(recentItem.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                                {
                                    File.Delete(lnkPath);
                                    break;
                                }
                            }
                        }

                        LoadRecentDocuments();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to remove recent document shortcut: {ex.Message}");
                    }
                };

                flyout.Items.Add(removeItem);
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
        private Point _dragStartPoint;
        private bool _isAppDragging = false;

        private void AppCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && (element.Tag as AppItem ?? element.DataContext as AppItem) is AppItem app)
            {
                bool isGlobalHidden = !SettingsEngine.Shell_StartMenuShowAppLabels;

                var parentGrid = FindVisualParent<GridView>(element);

                bool isPinnedArea = parentGrid != null &&
                                    parentGrid.Name != "SearchAndAllAppsGrid" &&
                                    parentGrid.Name != "SearchAndAllAppsGrid2" &&
                                    parentGrid.Name != "SearchAndAllAppsGrid3" &&
                                    parentGrid.Name != "RecentDocsGrid" &&
                                    parentGrid.Name != "OverlayFolderGrid";

                if (isGlobalHidden && app.ExecutablePath != "PINNED_FOLDER" && isPinnedArea)
                {
                    if (GlobalHoverText1 != null) GlobalHoverText1.Text = app.Name;
                    if (GlobalHoverText2 != null) GlobalHoverText2.Text = app.Name;

                    if (GlobalHoverBadge1 != null) FadeElement(GlobalHoverBadge1, 1.0, 150);
                    if (GlobalHoverBadge2 != null) FadeElement(GlobalHoverBadge2, 1.0, 150);
                }

                if (app.ExecutablePath == "PINNED_FOLDER") return;
            }

            if (sender is StackPanel panel)
                panel.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            else if (sender is Border border)
                border.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        }

        private void AppCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (GlobalHoverBadge1 != null && GlobalHoverBadge1.Opacity > 0)
                FadeElement(GlobalHoverBadge1, 0.0, 250);

            if (GlobalHoverBadge2 != null && GlobalHoverBadge2.Opacity > 0)
                FadeElement(GlobalHoverBadge2, 0.0, 250);

            if (sender is FrameworkElement element && (element.Tag as AppItem ?? element.DataContext as AppItem) is AppItem app)
            {
                if (app.ExecutablePath == "PINNED_FOLDER") return;
            }

            if (sender is StackPanel panel)
                panel.Background = new SolidColorBrush(Colors.Transparent);
            else if (sender is Border border)
                border.Background = new SolidColorBrush(Colors.Transparent);
        }

        private void AppCard_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var app = element.Tag as AppItem ?? element.DataContext as AppItem;
                if (app == null || app == _placeholderItem) return;

                if (!e.GetCurrentPoint(element).Properties.IsLeftButtonPressed) return;

                _sourceGrid = FindVisualParent<GridView>(element);

                if (_sourceGrid == null || (_sourceGrid.Name != null && _sourceGrid.Name.Contains("Search")) || _sourceGrid.Name == "RecentDocsGrid")
                    return;

                _draggedAppItem = app;
                _sourceFolderItem = null;

                var parentCat = _sourceGrid.DataContext as AppCategory;
                _sourceCategory = (parentCat != null && parentCat.IsTabbed && parentCat.Tabs.Count > parentCat.SelectedTabIndex)
                                  ? parentCat.Tabs[parentCat.SelectedTabIndex]
                                  : parentCat;

                if (_sourceCategory == null)
                {
                    _sourceCategory = GetAllCategoriesFlattened().FirstOrDefault(c => c.Apps.Contains(app));

                    if (_sourceCategory == null)
                    {
                        foreach (var cat in GetAllCategoriesFlattened())
                        {
                            foreach (var pinnedApp in cat.Apps)
                            {
                                if (pinnedApp.FolderApps.Contains(app))
                                {
                                    _sourceFolderItem = pinnedApp;
                                    _sourceCategory = cat;
                                    break;
                                }
                            }
                            if (_sourceFolderItem != null) break;
                        }
                    }
                }

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

                    if (_sourceFolderItem != null)
                    {
                        _sourceFolderItem.FolderApps.Remove(_draggedAppItem);
                    }
                    else
                    {
                        int idx = _sourceCategory.Apps.IndexOf(_draggedAppItem);
                        if (idx != -1) _sourceCategory.Apps[idx] = _placeholderItem;
                    }
                }
            }

            if (_isAppDragging && _dragGhost != null)
            {
                Canvas.SetLeft(_dragGhost, pt.X - 40);
                Canvas.SetTop(_dragGhost, pt.Y - 48);

                var (targetGrid, targetCategory, targetIndex, mergeTarget) = GetHoveredDropTarget(pt);

                if (targetGrid != null && targetCategory != null)
                {
                    var currentCategory = GetAllCategoriesFlattened().FirstOrDefault(c => c.Apps.Contains(_placeholderItem)) ?? _sourceCategory;

                    if (mergeTarget == null)
                    {
                        int currentIndex = currentCategory.Apps.IndexOf(_placeholderItem);
                        if (currentCategory != targetCategory || currentIndex != targetIndex)
                        {
                            currentCategory.Apps.Remove(_placeholderItem);
                            if (targetIndex > targetCategory.Apps.Count) targetIndex = targetCategory.Apps.Count;
                            if (targetIndex < 0) targetIndex = 0;
                            targetCategory.Apps.Insert(targetIndex, _placeholderItem);
                        }
                    }
                }
            }
        }

        private void MenuContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_draggedAppItem != null)
            {
                try
                {
                    MenuContainer.ReleasePointerCapture(e.Pointer);
                }
                catch { }

                try
                {
                    if (_isAppDragging)
                    {
                        if (_dragGhost != null)
                        {
                            DragCanvas.Children.Remove(_dragGhost);
                            _dragGhost = null;
                        }

                        var pt = e.GetCurrentPoint(MenuContainer).Position;
                        var (targetGrid, targetCategory, targetIndex, mergeTarget) = GetHoveredDropTarget(pt);

                        var placeholderCategory = GetAllCategoriesFlattened().FirstOrDefault(c => c.Apps.Contains(_placeholderItem));

                        if (mergeTarget != null && targetCategory != null && mergeTarget != _draggedAppItem)
                        {
                            placeholderCategory?.Apps.Remove(_placeholderItem);

                            if (mergeTarget.ExecutablePath == "PINNED_FOLDER")
                            {
                                mergeTarget.FolderApps.Add(_draggedAppItem);
                            }
                            else
                            {
                                int mergeIdx = targetCategory.Apps.IndexOf(mergeTarget);
                                if (mergeIdx != -1)
                                {
                                    int currentFolderSize = SettingsEngine.Shell_StartMenuFolderSize;

                                    var existingFolder = targetCategory.Apps.FirstOrDefault(a => a.ExecutablePath == "PINNED_FOLDER");
                                    if (existingFolder != null)
                                    {
                                        currentFolderSize = existingFolder.FolderSize;
                                    }

                                    var folderItem = new AppItem
                                    {
                                        Name = " ",
                                        ExecutablePath = "PINNED_FOLDER",
                                        FallbackGlyph = "",
                                        IsUwp = false,
                                        FolderSize = currentFolderSize
                                    };
                                    folderItem.FolderApps.Add(mergeTarget);
                                    folderItem.FolderApps.Add(_draggedAppItem);
                                    targetCategory.Apps[mergeIdx] = folderItem;
                                }
                            }
                        }
                        else
                        {
                            if (placeholderCategory != null)
                            {
                                int idx = placeholderCategory.Apps.IndexOf(_placeholderItem);
                                placeholderCategory.Apps[idx] = _draggedAppItem;
                            }
                            else
                            {
                                _sourceCategory?.Apps.Add(_draggedAppItem);
                            }
                        }

                        if (_sourceFolderItem != null)
                        {
                            if (_sourceFolderItem.FolderApps.Count == 1)
                            {
                                var remainingApp = _sourceFolderItem.FolderApps[0];
                                foreach (var cat in GetAllCategoriesFlattened())
                                {
                                    int fIdx = cat.Apps.IndexOf(_sourceFolderItem);
                                    if (fIdx != -1)
                                    {
                                        cat.Apps[fIdx] = remainingApp;
                                        break;
                                    }
                                }
                            }
                            else if (_sourceFolderItem.FolderApps.Count == 0)
                            {
                                foreach (var cat in GetAllCategoriesFlattened())
                                {
                                    cat.Apps.Remove(_sourceFolderItem);
                                }
                            }
                        }

                        SafeSavePins();
                    }
                    else
                    {
                        if (_draggedAppItem.ExecutablePath == "PINNED_FOLDER" && _sourceGrid != null)
                        {
                            var container = _sourceGrid.ContainerFromItem(_draggedAppItem) as FrameworkElement;
                            OpenPinnedFolder(_draggedAppItem, container ?? _sourceGrid);
                        }
                        else
                        {
                            LaunchApp(_draggedAppItem, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Pointer Release Error Handled] {ex.Message}");
                }
                finally
                {
                    _draggedAppItem = null;
                    _sourceCategory = null;
                    _sourceFolderItem = null;
                    _sourceGrid = null;
                    _isAppDragging = false;
                }
            }
        }

        private (GridView? grid, AppCategory? category, int index, AppItem? mergeTarget) GetHoveredDropTarget(Point pointerPos)
        {
            foreach (var grid in GetAllCategoryGrids())
            {
                if (grid.Visibility != Visibility.Visible) continue;

                var transform = grid.TransformToVisual(MenuContainer);
                var bounds = transform.TransformBounds(new Rect(0, 0, grid.ActualWidth, grid.ActualHeight));

                bounds.X -= 10; bounds.Y -= 10; bounds.Width += 20; bounds.Height += 40;

                if (bounds.Contains(pointerPos))
                {
                    int targetIndex = grid.Items.Count;
                    double closestDist = double.MaxValue;
                    AppItem? mergeTarget = null;
                    int visibleCount = 0;

                    for (int i = 0; i < grid.Items.Count; i++)
                    {
                        var item = grid.Items[i] as AppItem;
                        if (item == _placeholderItem) continue;

                        if (grid.ContainerFromIndex(i) is FrameworkElement itemContainer)
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

                                double mergeThreshold = Math.Pow(itemBounds.Width * 0.35, 2);
                                if (dist < mergeThreshold) mergeTarget = item;
                                else mergeTarget = null;
                            }
                            visibleCount++;
                        }
                    }

                    var cat = grid.DataContext as AppCategory;
                    if (cat == null)
                    {
                        if (grid.Name == "ProductivityAppsGrid" && PinnedCategories.Count > 0) cat = PinnedCategories[0];
                        else if (grid.Name == "SecondaryAppsGrid" && PinnedCategories.Count > 1) cat = PinnedCategories[1];
                    }

                    cat = GetEffectiveTargetCategory(cat!);

                    return (grid, cat, targetIndex, mergeTarget);
                }
            }
            return (null, null, -1, null);
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
                var image = new Image
                {
                    Source = _draggedAppItem.IconSource,
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5),
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

        private void AppGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (sender is GridView gridView)
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    EvaluateChevronVisibility(gridView, gridView.ActualWidth);
                });
            }
        }

        private void EvaluateChevronVisibility(GridView gridView, double width)
        {
            if (width <= 0) width = gridView.ActualWidth;
            if (width <= 0) return;

            if (gridView.Parent is StackPanel panel)
            {
                int gridIndex = panel.Children.IndexOf(gridView);

                if (gridIndex == 1 && panel.Children[0] is Grid headerGrid && headerGrid.Children.Count > 1 && headerGrid.Children[1] is ToggleButton chevronBtn)
                {
                    int itemsPerRow = (int)(width / 88.0);
                    if (itemsPerRow < 1) itemsPerRow = 6;

                    bool largeFolderOnFirstRow = false;
                    int currentSlot = 0;

                    foreach (var item in gridView.Items)
                    {
                        bool isLarge = (item is AppItem app && app.ExecutablePath == "PINNED_FOLDER" && app.FolderSize == 2);
                        int span = isLarge ? 2 : 1;

                        if (currentSlot + span > itemsPerRow)
                        {
                            break;
                        }

                        if (isLarge)
                        {
                            largeFolderOnFirstRow = true;
                            break;
                        }

                        currentSlot += span;
                    }

                    double baseHeight = largeFolderOnFirstRow ? 316.0 : 104.0;
                    double fullHeight = baseHeight;

                    if (gridView.ItemsPanelRoot is Panel panelRoot)
                    {
                        panelRoot.Measure(new Size(width, 10000.0));
                        if (panelRoot.DesiredSize.Height > 0)
                        {
                            fullHeight = panelRoot.DesiredSize.Height;
                        }
                    }

                    gridView.Tag = new double[] { baseHeight, fullHeight };

                    if (fullHeight > baseHeight + 5)
                    {
                        chevronBtn.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        chevronBtn.Visibility = Visibility.Collapsed;
                        if (chevronBtn.IsChecked == true) chevronBtn.IsChecked = false;
                    }

                    if (chevronBtn.IsChecked != true)
                    {
                        gridView.ClearValue(FrameworkElement.HeightProperty);
                        gridView.MaxHeight = baseHeight;
                    }
                }
                else if (gridIndex == 3)
                {
                    gridView.ClearValue(FrameworkElement.HeightProperty);
                    gridView.ClearValue(FrameworkElement.MaxHeightProperty);
                }
            }
        }

        public void SetGlobalFolderSize(int newSize)
        {
            SettingsEngine.Shell_StartMenuFolderSize = newSize;

            foreach (var cat in GetAllCategoriesFlattened())
            {
                foreach (var app in cat.Apps)
                {
                    if (app.ExecutablePath == "PINNED_FOLDER")
                    {
                        app.FolderSize = newSize;
                    }
                }
            }

            foreach (var grid in GetAllCategoryGrids())
            {
                if (grid.ItemsPanelRoot is StartMenuWrapPanel wrapPanel)
                {
                    wrapPanel.InvalidateMeasure();
                    wrapPanel.InvalidateArrange();
                }
            }

            SafeSavePins();
        }

        public void SetGlobalAppLabelVisibility(bool showLabels)
        {
            SettingsEngine.Shell_StartMenuShowAppLabels = showLabels;
            var targetVisibility = showLabels ? Visibility.Visible : Visibility.Collapsed;

            foreach (var cat in GetAllCategoriesFlattened())
            {
                foreach (var app in cat.Apps)
                {
                    if (app.ExecutablePath == "PINNED_FOLDER")
                    {
                        app.AppNameVisibility = Visibility.Visible;
                    }
                    else
                    {
                        app.AppNameVisibility = targetVisibility;
                    }

                    foreach (var folderApp in app.FolderApps)
                    {
                        folderApp.AppNameVisibility = Visibility.Visible;
                    }
                }
            }
        }

        /*public void SetGlobalAppLabelVisibility(bool showLabels)
        {
            SettingsEngine.Shell_StartMenuShowAppLabels = showLabels;
            var targetVisibility = showLabels ? Visibility.Visible : Visibility.Collapsed;

            foreach (var cat in GetAllCategoriesFlattened())
            {
                foreach (var app in cat.Apps)
                {
                    app.AppNameVisibility = targetVisibility;

                    foreach (var folderApp in app.FolderApps)
                    {
                        folderApp.AppNameVisibility = targetVisibility;
                    }
                }
            }

            // Recent Docs, All Apps, and Search Results
            foreach (var app in RecentDocsCollection) app.AppNameVisibility = targetVisibility;
            foreach (var app in AllAppsCollection) app.AppNameVisibility = targetVisibility;
            foreach (var app in SearchAppsCollection) app.AppNameVisibility = targetVisibility;
            foreach (var app in SearchSettingsCollection) app.AppNameVisibility = targetVisibility;
        }*/

        private IEnumerable<GridView> GetAllCategoryGrids()
        {
            var grids = new List<GridView>();

            if (PagesFlipView != null)
            {
                for (int i = 0; i < Pages.Count; i++)
                {
                    var container = PagesFlipView.ContainerFromIndex(i) as FrameworkElement;
                    if (container != null) FindAllGridViewsRecursive(container, grids);
                }
            }

            if (PagesFlipView2 != null)
            {
                for (int i = 0; i < Pages.Count; i++)
                {
                    var container = PagesFlipView2.ContainerFromIndex(i) as FrameworkElement;
                    if (container != null) FindAllGridViewsRecursive(container, grids);
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
                if (child is GridView gv) results.Add(gv);
                FindAllGridViewsRecursive(child, results);
            }
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

        #region Open Folder Popup Engine
        private bool _isFolderWindowOpen = false;
        public bool IsFolderWindowOpen => _isFolderWindowOpen;

        private bool _isClosingFolderOverlay = false;
        private AppItem? _activeFolderApp;

        private void OpenPinnedFolder(AppItem app, FrameworkElement container)
        {
            _activeFolderApp = app;
            if (OverlayFolderNameBox != null)
                OverlayFolderNameBox.Text = string.IsNullOrWhiteSpace(app.Name) || app.Name == " " ? (LocalizationService.Instance.GetString("StartMenu_DefaultFolderName") ?? "Folder") : app.Name;

            if (OverlayFolderGrid != null)
                OverlayFolderGrid.ItemsSource = app.FolderApps;

            FolderDots.Clear();
            int pageCount = (int)Math.Ceiling((double)app.FolderApps.Count / _folderItemsPerPage);
            if (pageCount == 0) pageCount = 1;

            for (int i = 0; i < pageCount; i++)
            {
                FolderDots.Add(new FolderDot { PageIndex = i, Opacity = (i == 0) ? 1.0 : 0.4 });
            }
            _currentFolderPage = 0;

            if (PinnedFolderOverlay != null)
            {
                PinnedFolderOverlay.Visibility = Visibility.Visible;
                FolderOverlayEnterAnimation.Begin();
            }

            _isFolderWindowOpen = true;
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

        private void OverlayFolderNameBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveOverlayFolderName();
        }

        private void SaveOverlayFolderName()
        {
            if (_activeFolderApp != null && OverlayFolderNameBox.Text != _activeFolderApp.Name)
            {
                _activeFolderApp.Name = OverlayFolderNameBox.Text;

                SafeSavePins();
            }
        }

        private async void CloseFolderOverlay()
        {
            if (PinnedFolderOverlay == null || PinnedFolderOverlay.Visibility == Visibility.Collapsed || _isClosingFolderOverlay)
                return;

            _isClosingFolderOverlay = true;

            FolderOverlayExitAnimation.Begin();

            _isFolderWindowOpen = false;
            _activeFolderApp = null;
            SafeSavePins();

            await Task.Delay(150);

            PinnedFolderOverlay.Visibility = Visibility.Collapsed;
            _isClosingFolderOverlay = false;
        }

        private void CloseFolderOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (object.ReferenceEquals(e.OriginalSource, PinnedFolderOverlay))
            {
                CloseFolderOverlay();
            }
        }

        private void OverlayFolderGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app)
            {
                LaunchApp(app, false);
                CloseFolderOverlay();
            }
        }

        private void OverlayFolderGrid_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count > 0 && e.Items[0] is AppItem app && _activeFolderApp != null)
            {
                e.Data.Properties.Add("DraggedApp", app);
                e.Data.Properties.Add("SourceFolder", _activeFolderApp);
                e.Data.SetText(app.Name ?? (LocalizationService.Instance.GetString("StartMenu_DefaultAppName") ?? "App"));
                e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            }
        }

        private void OverlayFolderGrid_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (_activeFolderApp != null)
            {
                if (_activeFolderApp.FolderApps.Count <= 1)
                {
                    CloseFolderOverlay();
                }
                else
                {
                    SafeSavePins();
                }
            }
        }

        private void FolderPopupCard_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;

            if (delta < 0 && _currentFolderPage < FolderDots.Count - 1)
            {
                ChangeFolderPage(_currentFolderPage + 1);
            }
            else if (delta > 0 && _currentFolderPage > 0)
            {
                ChangeFolderPage(_currentFolderPage - 1);
            }

            e.Handled = true;
        }

        private void FolderPageDotIndicator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int pageIndex)
            {
                ChangeFolderPage(pageIndex);
            }
        }

        private void ChangeFolderPage(int newPageIndex)
        {
            _currentFolderPage = newPageIndex;

            foreach (var dot in FolderDots)
            {
                dot.Opacity = (dot.PageIndex == _currentFolderPage) ? 1.0 : 0.4;
            }

            var sv = GetScrollViewer(OverlayFolderGrid);
            if (sv != null)
            {
                sv.ChangeView(null, _currentFolderPage * 288.0, null, false);
            }
        }

        private void ManageTabs_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && (item.Tag as AppCategory ?? item.DataContext as AppCategory) is AppCategory group)
            {
                AppCategory? parentGroup = group.IsTabbed ? group : Pages.SelectMany(p => p.PinnedCategories).FirstOrDefault(c => c.IsTabbed && c.Tabs != null && c.Tabs.Contains(group));

                if (parentGroup != null)
                {
                    var managerWindow = new TabbedGroupManagerWindow(parentGroup, () =>
                    {
                        SafeSavePins();
                    });

                    managerWindow.Activate();
                    HideMenu();
                }
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

        private void FolderName_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed) return;

            if (sender is Grid grid && grid.Tag is AppItem app)
            {
                if (app.ExecutablePath == "PINNED_FOLDER")
                {
                    var parentGridView = FindVisualParent<GridView>(grid);
                    var container = parentGridView?.ContainerFromItem(app) as FrameworkElement;
                    OpenPinnedFolder(app, container ?? grid);
                }
                else
                {
                    LaunchApp(app, false);
                }
                e.Handled = true;
            }
        }

        private void FolderName_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.Tag is AppItem app && app.ExecutablePath == "PINNED_FOLDER")
            {
                grid.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            }
        }

        private void FolderName_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is Grid grid && args.NewValue is AppItem app)
            {
                if (app.ExecutablePath == "PINNED_FOLDER")
                {
                    grid.HorizontalAlignment = HorizontalAlignment.Center;
                    grid.Width = app.FolderBoxSize;
                }
                else
                {
                    grid.HorizontalAlignment = HorizontalAlignment.Stretch;
                    grid.Width = double.NaN;
                }
            }
        }

        private void FolderName_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.Tag is AppItem app && app.ExecutablePath == "PINNED_FOLDER")
            {
                grid.Background = new SolidColorBrush(Colors.Transparent);
            }
        }

        private void InnerFolderApp_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app) LaunchApp(app, false);
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
            if (DesignSplitStandard == null) return;

            if (sender is MenuFlyoutItem item && item.Tag is string tag)
            {
                _currentSearchFilter = tag;

                string query = DesignSplitStandard.Visibility == Visibility.Visible ? (SearchBox1?.Text ?? "") : (SearchBox2?.Text ?? "");
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

                if (SearchAndAllAppsGrid2 != null) SearchAndAllAppsGrid2.ItemsSource = AllAppsCollection;

                if (DefaultRightPane1 != null) DefaultRightPane1.Visibility = Visibility.Visible;
                if (SearchRightPane1 != null) SearchRightPane1.Visibility = Visibility.Collapsed;

                if (_currentStyle == "SplitGrouped" || _currentStyle == "Compact")
                {
                    if (SearchOverlay2 != null) SearchOverlay2.Visibility = Visibility.Collapsed;
                    if (MainSplitContentGrid != null) MainSplitContentGrid.Visibility = Visibility.Visible;
                }

                return;
            }

            _isSearchAppsExpanded = false;
            if (SearchAppsMoreText != null) SearchAppsMoreText.Text = LocalizationService.Instance.GetString("StartMenu_More") ?? "More";
            if (SearchAppsMoreIcon?.RenderTransform is RotateTransform r1) FactoryAnimation.AnimateRotation(r1, 0);
            if (SearchAppsGrid != null) FactoryAnimation.AnimatePanelExpansion(SearchAppsGrid, false, 116);

            _isSearchSettingsExpanded = false;
            if (SearchSettingsMoreText != null) SearchSettingsMoreText.Text = LocalizationService.Instance.GetString("StartMenu_More") ?? "More";
            if (SearchSettingsMoreIcon?.RenderTransform is RotateTransform r2) FactoryAnimation.AnimateRotation(r2, 0);
            if (SearchSettingsGrid != null) FactoryAnimation.AnimatePanelExpansion(SearchSettingsGrid, false, 208);

            if (WebSearchHintText != null) WebSearchHintText.Text = $"{query} - Show web results";

            ViewModel.PerformSearch(query, _currentSearchFilter, DispatcherQueue, (fileItem) =>
            {
                if (SearchResultsCollection.Count == 2)
                {
                    if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.SelectedIndex = 0;
                    if (SearchAndAllAppsGrid2 != null) SearchAndAllAppsGrid2.SelectedIndex = 0;
                    UpdateSearchDetailsPane(fileItem);
                }
            });

            if (_currentStyle == "Standard" || _currentStyle == "SplitStandard")
            {
                if (PagesFlipView != null) PagesFlipView.Visibility = Visibility.Collapsed;
                if (BottomNavigationGrid != null) BottomNavigationGrid.Visibility = Visibility.Collapsed;

                if (SearchAndAllAppsGrid != null)
                {
                    SearchAndAllAppsGrid.Visibility = Visibility.Visible;
                    SearchAndAllAppsGrid.ItemsSource = SearchResultsCollection;
                }
                if (DefaultRightPane1 != null) DefaultRightPane1.Visibility = Visibility.Collapsed;
                if (SearchRightPane1 != null) SearchRightPane1.Visibility = Visibility.Visible;
            }
            else if (_currentStyle == "SplitGrouped" || _currentStyle == "Compact")
            {
                if (MainSplitContentGrid != null) MainSplitContentGrid.Visibility = Visibility.Collapsed;
                if (SearchOverlay2 != null) SearchOverlay2.Visibility = Visibility.Visible;
            }

            if (SearchResultsCollection.Count > 0)
            {
                if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.SelectedIndex = 0;
                if (SearchAndAllAppsGrid2 != null) SearchAndAllAppsGrid2.SelectedIndex = 0;
                UpdateSearchDetailsPane(SearchResultsCollection.First());
            }
            else
            {
                _currentSearchItem = null;
                string noResultsTxt = LocalizationService.Instance.GetString("StartMenu_SearchNoResults");
                if (SearchDetailsName1 != null) SearchDetailsName1.Text = noResultsTxt;
                if (SearchDetailsIcon1 != null) SearchDetailsIcon1.Source = null;
                if (AdminBtn1 != null) AdminBtn1.Visibility = Visibility.Collapsed;
                if (LocationBtn1 != null) LocationBtn1.Visibility = Visibility.Collapsed;
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
            if (SearchDetailsIcon1 != null) SearchDetailsIcon1.Source = item.IconSource;

            bool isSpecial = item.ExecutablePath?.StartsWith("WEB_SEARCH:") == true || item.ExecutablePath?.StartsWith("FILE_SEARCH:") == true;
            bool isUwpApp = item.IsUwp;

            if (AdminBtn1 != null) AdminBtn1.Visibility = (!isUwpApp && !isSpecial) ? Visibility.Visible : Visibility.Collapsed;
            if (LocationBtn1 != null) LocationBtn1.Visibility = (!isSpecial) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AppGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app)
            {
                bool isAllAppsFolder = app.FallbackGlyph == "\xE8B7" || (!string.IsNullOrEmpty(app.ExecutablePath) && Directory.Exists(app.ExecutablePath));

                if (isAllAppsFolder && app.ExecutablePath != "PINNED_FOLDER")
                {
                    app.IsExpanded = !app.IsExpanded;

                    if (app.FolderApps.Count == 0 && !string.IsNullOrEmpty(app.ExecutablePath) && Directory.Exists(app.ExecutablePath))
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

                    var listContainer = (sender as ListView)?.ContainerFromItem(app) as FrameworkElement
                                     ?? (sender as GridView)?.ContainerFromItem(app) as FrameworkElement;

                    if (listContainer != null)
                    {
                        var chevron = FindDescendant<FontIcon>(listContainer, "ChevronIcon");
                        if (chevron != null && chevron.RenderTransform is RotateTransform transform)
                        {
                            FactoryAnimation.AnimateRotation(transform, app.IsExpanded ? 180 : 0);
                        }

                        var nestedFolderGrid = FindDescendant<ItemsControl>(listContainer, "NestedFolderGrid");
                        if (nestedFolderGrid != null)
                        {
                            FactoryAnimation.AnimatePanelExpansion(nestedFolderGrid, app.IsExpanded, 0);
                            if (app.IsExpanded)
                            {
                                DispatcherQueue.TryEnqueue(async () =>
                                {
                                    await Task.Delay(260);
                                    (listContainer as UIElement)?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
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

        private async void WebSearchHintBtn_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchBox2?.Text ?? "";
            if (!string.IsNullOrWhiteSpace(query))
            {
                await Launcher.LaunchUriAsync(new Uri($"https://www.google.com/search?q={Uri.EscapeDataString(query)}"));
                HideMenu();
            }
        }

        private void SearchAppsMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            _isSearchAppsExpanded = !_isSearchAppsExpanded;
            if (SearchAppsMoreText != null) SearchAppsMoreText.Text = _isSearchAppsExpanded ? (LocalizationService.Instance.GetString("StartMenu_Less") ?? "Less") : (LocalizationService.Instance.GetString("StartMenu_More") ?? "More");

            if (SearchAppsMoreIcon?.RenderTransform is RotateTransform r1) FactoryAnimation.AnimateRotation(r1, 0);
            if (SearchAppsGrid != null) FactoryAnimation.AnimatePanelExpansion(SearchAppsGrid, false, 116);
        }

        private void SearchSettingsMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            _isSearchSettingsExpanded = !_isSearchSettingsExpanded;
            if (SearchSettingsMoreText != null) SearchSettingsMoreText.Text = _isSearchSettingsExpanded ? (LocalizationService.Instance.GetString("StartMenu_Less") ?? "Less") : (LocalizationService.Instance.GetString("StartMenu_More") ?? "More");

            if (SearchSettingsMoreIcon?.RenderTransform is RotateTransform r2) FactoryAnimation.AnimateRotation(r2, 0);
            if (SearchSettingsGrid != null) FactoryAnimation.AnimatePanelExpansion(SearchSettingsGrid, false, 208);
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
                else if (app.ExecutablePath.StartsWith("ms-settings:"))
                {
                    await Launcher.LaunchUriAsync(new Uri(app.ExecutablePath));
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

        private IEnumerable<AppCategory> GetAllCategoriesFlattened()
        {
            return Pages.SelectMany(p => p.PinnedCategories)
                        .SelectMany(c => c.IsTabbed ? (IEnumerable<AppCategory>)c.Tabs : new[] { c });
        }

        private AppCategory? GetEffectiveTargetCategory(AppCategory? parentCategory)
        {
            if (parentCategory == null) return null;

            if (parentCategory.IsTabbed)
            {
                if (parentCategory.Tabs.Count == 0)
                {
                    parentCategory.Tabs.Add(new AppCategory
                    {
                        Name = LocalizationService.Instance.GetString("StartMenu_NewTab") ?? "Tab 1"
                    });
                }

                int index = Math.Clamp(parentCategory.SelectedTabIndex, 0, parentCategory.Tabs.Count - 1);
                return parentCategory.Tabs[index];
            }

            return parentCategory;
        }

        private void SafeSavePins()
        {
            var pageNames = string.Join("|", Pages.Select(p =>
                string.IsNullOrWhiteSpace(p.PageName) ? $"Page {p.PageIndex + 1}" : p.PageName
            ));
            SettingsEngine.StartMenuPageNames = pageNames;

            foreach (var page in Pages)
            {
                foreach (var cat in page.PinnedCategories)
                {
                    if (cat.IsTabbed)
                    {
                        cat.Apps.Clear();
                        cat.Apps.Add(new AppItem { Name = "TAB_FLAG", ExecutablePath = "TABBED_GROUP_FLAG", FallbackGlyph = "\xE8D5", IsUwp = false });

                        foreach (var tab in cat.Tabs)
                        {
                            var tabStorage = new AppItem
                            {
                                Name = string.IsNullOrWhiteSpace(tab.Name) ? "Tab" : tab.Name,
                                ExecutablePath = "TAB_DATA",
                                FallbackGlyph = "\xE8D5",
                                IsUwp = false
                            };

                            foreach (var app in tab.Apps)
                            {
                                tabStorage.FolderApps.Add(app);
                            }
                            cat.Apps.Add(tabStorage);
                        }
                    }
                    else
                    {
                        foreach (var app in cat.Apps)
                        {
                            if (app.ExecutablePath == "PINNED_FOLDER" && string.IsNullOrWhiteSpace(app.Name))
                            {
                                app.Name = " ";
                            }
                        }
                    }
                }
            }

            ViewModel.SaveStartMenuPins();

            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                foreach (var grid in GetAllCategoryGrids())
                {
                    if (grid.ItemsPanelRoot is StartMenuWrapPanel wrapPanel)
                    {
                        wrapPanel.InvalidateMeasure();
                        wrapPanel.InvalidateArrange();
                    }
                    EvaluateChevronVisibility(grid, grid.ActualWidth);
                }
            });
        }

        #endregion
    }
}