// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities.Animations;
using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using EvolveOS_ShellEnhancer.Utilities.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using WinRT.Interop;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using EvolveOS_ShellEnhancer.Models;
using System.Runtime.InteropServices;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class CustomStartMenuWindow : Window
    {
        #region Win32 P/Invoke for TopMost Enforcement
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        #endregion

        #region Fields & Properties
        public DisplayArea? TargetDisplayArea { get; set; }

        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private bool _isVisible = false;
        private bool _isDataLoaded = false;

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

        public ObservableCollection<AppCategory> PinnedCategories { get; } = new();
        public ObservableCollection<AppItem> RecentDocsCollection { get; } = new();
        public ObservableCollection<AppItem> AllAppsCollection { get; } = new();
        public ObservableCollection<AppItem> SearchResultsCollection { get; } = new();

        private string _currentSearchFilter = "Apps";
        private bool _isShowingAllApps = false;
        private AppItem? _currentSearchItem;

        private CancellationTokenSource? _searchCts;

        private FileSystemWatcher? _userStartMenuWatcher;
        private FileSystemWatcher? _systemStartMenuWatcher;
        private DispatcherTimer? _appRefreshDebounceTimer;

        private AccountCardWindow? _activeAccountCardWindow;
        private DateTime _lastAccountCardCloseTime = DateTime.MinValue;
        private string _currentUserEmail = string.Empty;
        private string _currentAccountType = "Local Account";
        private bool _ignoreDeactivation = false;
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

            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            Win32Helper.RemoveWindowBorders(_hWnd);
            TaskbarOverlayManager.ApplyWidgetStyles(_hWnd);

            _appWindow.Hide();

            this.Activated += OnWindowActivated;

            InitializeAppWatchers();
            LoadAppsData();
            LoadUserProfile();
        }

        private void InitializeAppWatchers()
        {
            try
            {
                _appRefreshDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _appRefreshDebounceTimer.Tick += (s, e) =>
                {
                    _appRefreshDebounceTimer.Stop();
                    _isDataLoaded = false;

                    LoadAppsData();
                };

                string userStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs");
                string systemStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);

                if (Directory.Exists(userStartMenu))
                {
                    _userStartMenuWatcher = new FileSystemWatcher(userStartMenu)
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    };
                    _userStartMenuWatcher.Created += OnStartMenuChanged;
                    _userStartMenuWatcher.Deleted += OnStartMenuChanged;
                    _userStartMenuWatcher.Renamed += OnStartMenuChanged;
                }

                if (Directory.Exists(systemStartMenu))
                {
                    _systemStartMenuWatcher = new FileSystemWatcher(systemStartMenu)
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    };
                    _systemStartMenuWatcher.Created += OnStartMenuChanged;
                    _systemStartMenuWatcher.Deleted += OnStartMenuChanged;
                    _systemStartMenuWatcher.Renamed += OnStartMenuChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize start menu watchers: {ex.Message}");
            }
        }

        private void OnStartMenuChanged(object sender, FileSystemEventArgs e)
        {
            if (e.FullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                e.FullPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(e.FullPath))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _appRefreshDebounceTimer?.Stop();
                    _appRefreshDebounceTimer?.Start();
                });
            }
        }

        private async void LoadUserProfile()
        {
            string displayName = Environment.UserName;
            string userEmail = string.Empty;
            ImageSource? profileImage = null;

            try
            {
                var users = await User.FindAllAsync();
                var user = users.FirstOrDefault();

                if (user != null)
                {
                    var nameObj = await user.GetPropertyAsync(KnownUserProperties.DisplayName);
                    if (nameObj != null && !string.IsNullOrWhiteSpace(nameObj.ToString()))
                    {
                        displayName = nameObj.ToString()!;
                    }

                    var emailObj = await user.GetPropertyAsync(KnownUserProperties.PrincipalName);
                    if (emailObj != null && !string.IsNullOrWhiteSpace(emailObj.ToString()))
                    {
                        userEmail = emailObj.ToString()!;
                    }

                    var picStreamRef = await user.GetPictureAsync(UserPictureSize.Size64x64);
                    if (picStreamRef != null)
                    {
                        using var stream = await picStreamRef.OpenReadAsync();
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(stream);
                        profileImage = bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load user profile: {ex.Message}");
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                _currentUserEmail = userEmail;

                if (!string.IsNullOrEmpty(userEmail) && userEmail.Contains("@"))
                {
                    _currentAccountType = "Microsoft Account";
                }
                else
                {
                    _currentAccountType = "Local Account";
                }

                if (UnifiedProfileName != null) UnifiedProfileName.Text = displayName;

                if (UnifiedProfilePic != null)
                {
                    UnifiedProfilePic.DisplayName = displayName;
                    if (profileImage != null) UnifiedProfilePic.ProfilePicture = profileImage;
                }

                bool enableProfile = SettingsEngine.Shell_StartMenuProfileClick;
                if (ProfileButton != null)
                {
                    ProfileButton.IsHitTestVisible = enableProfile;
                    ProfileButton.IsEnabled = enableProfile;
                }
            });
        }

        private async void LoadAppsData()
        {
            if (_isDataLoaded) return;
            _isDataLoaded = true;

            try
            {
                var fetchedAllApps = await StartMenuHelper.GetAllAppsAsync();

                if (fetchedAllApps.Count > 0)
                {
                    AllAppsCollection.Clear();
                    foreach (var app in fetchedAllApps) AllAppsCollection.Add(app);

                    PinnedCategories.Clear();

                    string savedPins = SettingsEngine.StartMenuPinnedApps;
                    if (!string.IsNullOrWhiteSpace(savedPins))
                    {
                        if (savedPins.Contains("|"))
                        {
                            var categories = savedPins.Split(';', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var catStr in categories)
                            {
                                var parts = catStr.Split('|');
                                if (parts.Length == 2)
                                {
                                    var cat = new AppCategory { Name = parts[0] };
                                    var pinNames = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var name in pinNames)
                                    {
                                        var match = fetchedAllApps.FirstOrDefault(a => a.Name == name);
                                        if (match != null) cat.Apps.Add(match);
                                    }
                                    PinnedCategories.Add(cat);
                                }
                            }
                        }
                        else
                        {
                            var defaultCat = new AppCategory { Name = "Pinned" };
                            var pinNames = savedPins.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var name in pinNames)
                            {
                                var match = fetchedAllApps.FirstOrDefault(a => a.Name == name);
                                if (match != null) defaultCat.Apps.Add(match);
                            }
                            PinnedCategories.Add(defaultCat);
                            SaveStartMenuPins();
                        }
                    }
                    else
                    {
                        var defaultCat = new AppCategory { Name = "Pinned" };
                        var preferredPins = new[] { "Edge", "Settings", "File Explorer", "Store", "Photos", "Camera", "Calculator", "Clock", "Terminal", "Spotify", "Discord", "Word", "Excel" };
                        var defaultPinned = fetchedAllApps.Where(a => preferredPins.Any(p => a.Name != null && a.Name.Contains(p, StringComparison.OrdinalIgnoreCase))).Take(12).ToList();

                        foreach (var app in defaultPinned) defaultCat.Apps.Add(app);
                        PinnedCategories.Add(defaultCat);
                    }

                    _ = ExtractIconsAsync(PinnedCategories.SelectMany(c => c.Apps).ToList());
                    _ = ExtractIconsAsync(AllAppsCollection);

                    if (ProductivityAppsGrid != null && PinnedCategories.Count > 0)
                        ProductivityAppsGrid.ItemsSource = PinnedCategories[0].Apps;
                    if (SecondaryAppsGrid != null && PinnedCategories.Count > 0)
                        SecondaryAppsGrid.ItemsSource = PinnedCategories[0].Apps;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load apps: {ex.Message}");
            }
        }

        private async Task ExtractIconsAsync(IEnumerable<AppItem> apps)
        {
            foreach (var app in apps)
            {
                if (app.IconSource == null)
                {
                    try
                    {
                        var icon = await StartMenuHelper.ExtractAppIconAsync(app);
                        if (icon != null)
                        {
                            app.IconSource = icon;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to set icon for {app.Name}: {ex.Message}");
                    }
                }
            }
        }

        public void LoadRecentDocuments()
        {
            RecentDocsCollection.Clear();

            if (RecentDocsPanel == null) return;

            if (!ShowRecentDocs)
            {
                RecentDocsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            RecentDocsPanel.Visibility = Visibility.Visible;
            try
            {
                string recentPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                if (Directory.Exists(recentPath))
                {
                    var recentFiles = new DirectoryInfo(recentPath).GetFiles("*.lnk")
                        .OrderByDescending(f => f.LastWriteTime)
                        .Take(10);

                    foreach (var file in recentFiles)
                    {
                        RecentDocsCollection.Add(new AppItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file.Name),
                            ExecutablePath = file.FullName,
                            FallbackGlyph = "\xE8A5",
                            IsUwp = false
                        });
                    }

                    _ = ExtractIconsAsync(RecentDocsCollection.ToList());
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Recent Docs Error: {ex.Message}"); }
        }
        #endregion

        #region Window & Layout Management
        public void ToggleVisibility()
        {
            if (!_isDataLoaded)
            {
                LoadAppsData();
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

            int windowWidth = 750;
            int windowHeight = 650;

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
            e.Handled = true;
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
            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:accounts"));
            }
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

                if ((DateTime.Now - _lastAccountCardCloseTime).TotalMilliseconds < 200)
                {
                    return;
                }

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

                        if (clickedOutsideBoth)
                        {
                            HideMenu();
                        }
                    });

                _activeAccountCardWindow.Activate();
            }
        }

        private void ProfileButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (SettingsEngine.Shell_StartMenuProfileClick)
            {
                ProfileGrowStoryboard.Begin();
            }
        }

        private void ProfileButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (SettingsEngine.Shell_StartMenuProfileClick)
            {
                ProfileShrinkStoryboard.Begin();
            }
        }

        private async void QuickFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string folder)
            {
                try
                {
                    if (folder == "Settings")
                    {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:"));
                    }
                    else if (folder == "Run")
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", "shell:::{2559a1f3-21d7-11d4-bdaf-00c04f60b9f0}") { UseShellExecute = true });
                    }
                    else
                    {
                        string? path = folder switch
                        {
                            "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "Pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                            "Music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                            "Downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                            _ => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                        };

                        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        {
                            await Launcher.LaunchFolderPathAsync(path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open quick folder/setting: {ex.Message}");
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

                    if (PinnedCategoriesControl != null) PinnedCategoriesControl.Visibility = Visibility.Collapsed;
                    if (SearchAndAllAppsGrid != null)
                    {
                        SearchAndAllAppsGrid.Visibility = Visibility.Visible;
                        SearchAndAllAppsGrid.ItemsSource = AllAppsCollection;
                    }

                    if (ProductivityAppsGrid != null) ProductivityAppsGrid.ItemsSource = AllAppsCollection;
                }
                else
                {
                    btn.Content = LocalizationService.Instance.GetString("StartMenu_AllApps");

                    if (PinnedCategoriesControl != null) PinnedCategoriesControl.Visibility = Visibility.Visible;
                    if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.Visibility = Visibility.Collapsed;

                    if (ProductivityAppsGrid != null && PinnedCategories.Count > 0)
                        ProductivityAppsGrid.ItemsSource = PinnedCategories[0].Apps;
                }
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            PinnedCategories.Add(new AppCategory { Name = "New Section" });
            SaveStartMenuPins();
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem btn && btn.Tag is AppCategory cat)
            {
                PinnedCategories.Remove(cat);
                SaveStartMenuPins();
            }
        }

        private void CategoryName_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveStartMenuPins();
        }

        private void CategoryName_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                this.Content.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        #endregion

        #region Context Menu Handlers (Pinning / Actions)

        private void AppCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is FrameworkElement element && element.DataContext is AppItem app)
            {
                if (app.ExecutablePath != null && (app.ExecutablePath.StartsWith("WEB_SEARCH:") || app.ExecutablePath.StartsWith("FILE_SEARCH:")))
                    return;

                MenuFlyout flyout = new MenuFlyout();

                bool isPinnedToStart = PinnedCategories.Any(c => c.Apps.Contains(app));
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
                        foreach (var cat in PinnedCategories) cat.Apps.Remove(app);
                    }
                    else
                    {
                        if (PinnedCategories.Count == 0) PinnedCategories.Add(new AppCategory { Name = "Pinned" });
                        PinnedCategories.First().Apps.Add(app);
                    }

                    SaveStartMenuPins();
                };
                flyout.Items.Add(pinStartItem);
                flyout.Items.Add(new MenuFlyoutSeparator());

                bool isPinnedToTaskbar = IsPinnedToTaskbar(app);
                var pinTaskbarItem = new MenuFlyoutItem
                {
                    Text = isPinnedToTaskbar
                        ? LocalizationService.Instance.GetString("StartMenu_ContextUnpinTaskbar")
                        : LocalizationService.Instance.GetString("StartMenu_ContextPinTaskbar"),
                    Icon = new FontIcon { Glyph = "\xE196" }
                };
                pinTaskbarItem.Click += (s, args) => ToggleTaskbarPin(app, isPinnedToTaskbar);
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

        private void ScrollViewer_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.Handled) return;

            if (sender is FrameworkElement element)
            {
                MenuFlyout flyout = new MenuFlyout();
                var addItem = new MenuFlyoutItem { Text = "Add Section" };
                addItem.Icon = new FontIcon { Glyph = "\xE710" };
                addItem.Click += AddCategory_Click;
                flyout.Items.Add(addItem);

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

        private void SaveStartMenuPins()
        {
            var categoryStrings = new List<string>();
            foreach (var cat in PinnedCategories)
            {
                var appNames = cat.Apps.Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n));
                categoryStrings.Add($"{cat.Name}|{string.Join(",", appNames)}");
            }
            SettingsEngine.StartMenuPinnedApps = string.Join(";", categoryStrings);
        }

        private string GetTaskbarFolderPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

        private bool IsPinnedToTaskbar(AppItem app)
        {
            if (string.IsNullOrEmpty(app.Name)) return false;
            string dir = GetTaskbarFolderPath();
            if (!Directory.Exists(dir)) return false;

            var shortcuts = Directory.GetFiles(dir, "*.lnk");
            foreach (var lnk in shortcuts)
            {
                if (Path.GetFileNameWithoutExtension(lnk).Equals(app.Name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void ToggleTaskbarPin(AppItem app, bool isPinned)
        {
            if (string.IsNullOrEmpty(app.Name) || string.IsNullOrEmpty(app.ExecutablePath)) return;
            string dir = GetTaskbarFolderPath();
            string lnkPath = Path.Combine(dir, $"{app.Name}.lnk");

            try
            {
                if (isPinned)
                {
                    if (File.Exists(lnkPath)) File.Delete(lnkPath);
                }
                else
                {
                    if (app.IsUwp)
                    {
                        IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                        IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(lnkPath);
                        shortcut.TargetPath = "explorer.exe";
                        shortcut.Arguments = $@"shell:appsFolder\{app.ExecutablePath}";
                        shortcut.Save();
                    }
                    else
                    {
                        IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                        IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(lnkPath);
                        shortcut.TargetPath = app.ExecutablePath;
                        shortcut.Save();
                    }
                }

                TaskbarManager.ReloadAll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Taskbar pin toggle failed: " + ex.Message);
            }
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
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            if (string.IsNullOrWhiteSpace(query))
            {
                if (_isShowingAllApps)
                {
                    if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.ItemsSource = AllAppsCollection;
                    if (ProductivityAppsGrid != null) ProductivityAppsGrid.ItemsSource = AllAppsCollection;
                    if (SecondaryAppsGrid != null) SecondaryAppsGrid.ItemsSource = AllAppsCollection;
                }
                else
                {
                    if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.Visibility = Visibility.Collapsed;
                    if (PinnedCategoriesControl != null) PinnedCategoriesControl.Visibility = Visibility.Visible;

                    if (ProductivityAppsGrid != null && PinnedCategories.Count > 0) ProductivityAppsGrid.ItemsSource = PinnedCategories[0].Apps;
                    if (SecondaryAppsGrid != null && PinnedCategories.Count > 0) SecondaryAppsGrid.ItemsSource = PinnedCategories[0].Apps;
                }

                if (DefaultRightPane1 != null) DefaultRightPane1.Visibility = Visibility.Visible;
                if (DefaultRightPane2 != null) DefaultRightPane2.Visibility = Visibility.Visible;
                if (SearchRightPane1 != null) SearchRightPane1.Visibility = Visibility.Collapsed;
                if (SearchRightPane2 != null) SearchRightPane2.Visibility = Visibility.Collapsed;
                return;
            }

            SearchResultsCollection.Clear();

            if (_currentSearchFilter == "Apps")
            {
                var results = AllAppsCollection
                    .Where(a => a.Name != null && a.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var item in results) SearchResultsCollection.Add(item);
            }
            else if (_currentSearchFilter == "Files")
            {
                SearchResultsCollection.Add(new AppItem
                {
                    Name = LocalizationService.Instance.GetString("StartMenu_SearchPCPrefix", query),
                    FallbackGlyph = "\xE8A5",
                    ExecutablePath = "FILE_SEARCH:" + query
                });

                Task.Run(() =>
                {
                    var searchPaths = new List<string>();
                    string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                    searchPaths.Add(Path.Combine(userPath, "Desktop"));
                    searchPaths.Add(Path.Combine(userPath, "Documents"));
                    searchPaths.Add(Path.Combine(userPath, "Downloads"));

                    foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                    {
                        searchPaths.Add(d.RootDirectory.FullName);
                    }

                    int resultsFound = 0;
                    const int maxResults = 15;

                    foreach (var path in searchPaths.Distinct())
                    {
                        if (token.IsCancellationRequested || resultsFound >= maxResults) break;

                        foreach (var file in SafeEnumerateFiles(path, query, token))
                        {
                            if (token.IsCancellationRequested || resultsFound >= maxResults) break;

                            resultsFound++;

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                var fileItem = new AppItem
                                {
                                    Name = Path.GetFileName(file),
                                    ExecutablePath = file,
                                    FallbackGlyph = "\xE8A5",
                                    IsUwp = false
                                };

                                int insertIndex = SearchResultsCollection.Count > 0 ? SearchResultsCollection.Count - 1 : 0;
                                SearchResultsCollection.Insert(insertIndex, fileItem);

                                _ = ExtractIconsAsync(new[] { fileItem });

                                if (SearchResultsCollection.Count == 2)
                                {
                                    if (SearchAndAllAppsGrid != null) SearchAndAllAppsGrid.SelectedIndex = 0;
                                    if (ProductivityAppsGrid != null) ProductivityAppsGrid.SelectedIndex = 0;
                                    if (SecondaryAppsGrid != null) SecondaryAppsGrid.SelectedIndex = 0;
                                    UpdateSearchDetailsPane(fileItem);
                                }
                            });
                        }
                    }
                }, token);
            }
            else if (_currentSearchFilter == "Web")
            {
                SearchResultsCollection.Add(new AppItem
                {
                    Name = LocalizationService.Instance.GetString("StartMenu_SearchWebPrefix", query),
                    FallbackGlyph = "\xE8FA",
                    ExecutablePath = "WEB_SEARCH:" + query
                });
            }

            if (PinnedCategoriesControl != null) PinnedCategoriesControl.Visibility = Visibility.Collapsed;
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
            if (e.ClickedItem is AppItem app) LaunchApp(app, false);
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

        private IEnumerable<string> SafeEnumerateFiles(string rootPath, string query, CancellationToken token)
        {
            var dirs = new Queue<string>();
            if (Directory.Exists(rootPath)) dirs.Enqueue(rootPath);

            while (dirs.Count > 0)
            {
                if (token.IsCancellationRequested) yield break;
                string currentDir = dirs.Dequeue();

                string[] files;
                try { files = Directory.GetFiles(currentDir, $"*{query}*", SearchOption.TopDirectoryOnly); }
                catch { files = Array.Empty<string>(); }

                foreach (var file in files)
                {
                    if (token.IsCancellationRequested) yield break;
                    yield return file;
                }

                string[] subDirs;
                try { subDirs = Directory.GetDirectories(currentDir); }
                catch { subDirs = Array.Empty<string>(); }

                foreach (var dir in subDirs)
                {
                    dirs.Enqueue(dir);
                }
            }
        }
    }
}