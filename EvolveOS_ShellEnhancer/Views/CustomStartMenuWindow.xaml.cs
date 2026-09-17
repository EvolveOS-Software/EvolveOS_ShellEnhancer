// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Utilities.Helpers;
using EvolveOS_ShellEnhancer.Utilities.Managers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class CustomStartMenuWindow : Window
    {
        #region Fields & Properties
        public DisplayArea? TargetDisplayArea { get; set; }

        private readonly AppWindow _appWindow;
        private readonly IntPtr _hWnd;
        private bool _isVisible = false;
        private bool _isDataLoaded = false;

        private string _currentAlignment = "Center";
        private string _currentPosition = "Bottom";
        private string _currentStyle = "SplitStandard";

        public ObservableCollection<AppItem> PinnedAppsCollection { get; } = new();
        public ObservableCollection<AppItem> AllAppsCollection { get; } = new();

        public ObservableCollection<AppItem> SearchResultsCollection { get; } = new();
        private string _currentSearchFilter = "Apps";
        private bool _isShowingAllApps = false;
        private AppItem? _currentSearchItem;

        private CancellationTokenSource? _searchCts;
        #endregion

        #region Initialization & Data Loading
        public CustomStartMenuWindow()
        {
            this.InitializeComponent();

            StandardPinnedAppsGrid.ItemsSource = PinnedAppsCollection;
            ProductivityAppsGrid.ItemsSource = PinnedAppsCollection;
            SecondaryAppsGrid.ItemsSource = PinnedAppsCollection;

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

            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            Win32Helper.RemoveWindowBorders(_hWnd);
            TaskbarOverlayManager.ApplyWidgetStyles(_hWnd);

            _appWindow.Hide();

            this.Activated += OnWindowActivated;

            LoadAppsData();
            LoadUserProfile();
        }

        private async void LoadUserProfile()
        {
            string displayName = Environment.UserName;
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
                if (ProfileName1 != null) ProfileName1.Text = displayName;
                if (ProfileName2 != null) ProfileName2.Text = displayName;

                if (ProfilePic1 != null)
                {
                    ProfilePic1.DisplayName = displayName;
                    if (profileImage != null) ProfilePic1.ProfilePicture = profileImage;
                }

                if (ProfilePic2 != null)
                {
                    ProfilePic2.DisplayName = displayName;
                    if (profileImage != null) ProfilePic2.ProfilePicture = profileImage;
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
                var fetchedPinnedApps = await StartMenuHelper.GetPinnedAppsAsync();

                if (fetchedAllApps.Count > 0)
                {
                    AllAppsCollection.Clear();
                    foreach (var app in fetchedAllApps) AllAppsCollection.Add(app);

                    PinnedAppsCollection.Clear();
                    foreach (var app in fetchedPinnedApps) PinnedAppsCollection.Add(app);

                    _ = ExtractIconsAsync(fetchedAllApps.Concat(fetchedPinnedApps).Distinct());
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

            if (isStandard || isGrouped)
            {
                MenuContainer.Width = 750;
                MenuContainer.Height = 650;
            }

            if (_isVisible) ShowMenu();
        }

        private void ShowMenu()
        {
            var displayArea = TargetDisplayArea ?? DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);

            int menuWidth = (int)MenuContainer.Width;
            int menuHeight = (int)MenuContainer.Height;
            int taskbarOffset = 60;
            int margin = 16;

            int x = 0; int y = 0;
            string targetPos = TaskbarManager.GetPositionForDisplay(displayArea.DisplayId.Value.ToString());

            switch (targetPos)
            {
                case "Top":
                    y = displayArea.OuterBounds.Y + taskbarOffset;
                    x = (_currentAlignment == "Center") ? displayArea.OuterBounds.X + (displayArea.OuterBounds.Width - menuWidth) / 2 : displayArea.OuterBounds.X + margin;
                    break;
                case "Left":
                    x = displayArea.OuterBounds.X + taskbarOffset;
                    y = (_currentAlignment == "Center") ? displayArea.OuterBounds.Y + (displayArea.OuterBounds.Height - menuHeight) / 2 : displayArea.OuterBounds.Y + margin;
                    break;
                case "Right":
                    x = displayArea.OuterBounds.X + displayArea.OuterBounds.Width - menuWidth - taskbarOffset;
                    y = (_currentAlignment == "Center") ? displayArea.OuterBounds.Y + (displayArea.OuterBounds.Height - menuHeight) / 2 : displayArea.OuterBounds.Y + margin;
                    break;
                case "Bottom":
                default:
                    y = displayArea.OuterBounds.Y + displayArea.OuterBounds.Height - menuHeight - taskbarOffset;
                    x = (_currentAlignment == "Center") ? displayArea.OuterBounds.X + (displayArea.OuterBounds.Width - menuWidth) / 2 : displayArea.OuterBounds.X + margin;
                    break;
            }

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, menuWidth, menuHeight));
            _appWindow.Show();
            _isVisible = true;

            TaskbarOverlayManager.EnsureTopmost(_hWnd);
        }

        private void HideMenu()
        {
            _appWindow.Hide();
            _isVisible = false;
            App.LastStartMenuCloseTime = DateTime.Now;
        }
        #endregion

        #region UI Event Handlers
        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
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
        }

        private void PowerRestart_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true });
        }

        private void PowerSleep_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0") { CreateNoWindow = true });
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
                    btn.Content = "< Back to Pinned";
                    StandardPinnedAppsGrid.ItemsSource = AllAppsCollection;
                    ProductivityAppsGrid.ItemsSource = AllAppsCollection;
                }
                else
                {
                    btn.Content = "All apps >";
                    StandardPinnedAppsGrid.ItemsSource = PinnedAppsCollection;
                    ProductivityAppsGrid.ItemsSource = PinnedAppsCollection;
                }
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

            if (sender is MenuFlyoutItem item)
            {
                _currentSearchFilter = item.Text;

                string query = DesignSplitStandard.Visibility == Visibility.Visible ? SearchBox1.Text : SearchBox2.Text;
                PerformSearch(query);
            }
        }

        private void PerformSearch(string query)
        {
            if (StandardPinnedAppsGrid == null || ProductivityAppsGrid == null || SecondaryAppsGrid == null) return;

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            if (string.IsNullOrWhiteSpace(query))
            {
                var activeCollection = _isShowingAllApps ? AllAppsCollection : PinnedAppsCollection;
                StandardPinnedAppsGrid.ItemsSource = activeCollection;
                ProductivityAppsGrid.ItemsSource = activeCollection;
                SecondaryAppsGrid.ItemsSource = activeCollection;

                DefaultRightPane1.Visibility = Visibility.Visible;
                DefaultRightPane2.Visibility = Visibility.Visible;
                SearchRightPane1.Visibility = Visibility.Collapsed;
                SearchRightPane2.Visibility = Visibility.Collapsed;
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
                    Name = $"Search entire PC for '{query}'",
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
                                    StandardPinnedAppsGrid.SelectedIndex = 0;
                                    ProductivityAppsGrid.SelectedIndex = 0;
                                    SecondaryAppsGrid.SelectedIndex = 0;
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
                    Name = $"Search Web for '{query}'",
                    FallbackGlyph = "\xE8FA",
                    ExecutablePath = "WEB_SEARCH:" + query
                });
            }

            StandardPinnedAppsGrid.ItemsSource = SearchResultsCollection;
            ProductivityAppsGrid.ItemsSource = SearchResultsCollection;
            SecondaryAppsGrid.ItemsSource = SearchResultsCollection;

            DefaultRightPane1.Visibility = Visibility.Collapsed;
            DefaultRightPane2.Visibility = Visibility.Collapsed;
            SearchRightPane1.Visibility = Visibility.Visible;
            SearchRightPane2.Visibility = Visibility.Visible;

            if (SearchResultsCollection.Count > 0)
            {
                StandardPinnedAppsGrid.SelectedIndex = 0;
                ProductivityAppsGrid.SelectedIndex = 0;
                SecondaryAppsGrid.SelectedIndex = 0;
                UpdateSearchDetailsPane(SearchResultsCollection.First());
            }
            else
            {
                _currentSearchItem = null;
                SearchDetailsName1.Text = "No results found";
                SearchDetailsName2.Text = "No results found";
                SearchDetailsIcon1.Source = null;
                SearchDetailsIcon2.Source = null;
                AdminBtn1.Visibility = Visibility.Collapsed;
                LocationBtn1.Visibility = Visibility.Collapsed;
                AdminBtn2.Visibility = Visibility.Collapsed;
                LocationBtn2.Visibility = Visibility.Collapsed;
            }
        }

        private void AppGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is AppItem item && SearchRightPane1.Visibility == Visibility.Visible)
            {
                UpdateSearchDetailsPane(item);
            }
        }

        private void UpdateSearchDetailsPane(AppItem item)
        {
            _currentSearchItem = item;
            SearchDetailsName1.Text = item.Name;
            SearchDetailsName2.Text = item.Name;
            SearchDetailsIcon1.Source = item.IconSource;
            SearchDetailsIcon2.Source = item.IconSource;

            bool isSpecial = item.ExecutablePath?.StartsWith("WEB_SEARCH:") == true || item.ExecutablePath?.StartsWith("FILE_SEARCH:") == true;
            bool isUwpApp = item.IsUwp;

            AdminBtn1.Visibility = (!isUwpApp && !isSpecial) ? Visibility.Visible : Visibility.Collapsed;
            LocationBtn1.Visibility = (!isSpecial) ? Visibility.Visible : Visibility.Collapsed;

            AdminBtn2.Visibility = (!isUwpApp && !isSpecial) ? Visibility.Visible : Visibility.Collapsed;
            LocationBtn2.Visibility = (!isSpecial) ? Visibility.Visible : Visibility.Collapsed;
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