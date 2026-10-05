// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Windows.System;

namespace EvolveOS_ShellEnhancer.ViewModels
{
    public class CustomStartMenuViewModel : INotifyPropertyChanged
    {
        #region Fields & Properties
        public ObservableCollection<StartMenuPage> Pages { get; } = new();
        public ObservableCollection<AppCategory> PinnedCategories { get; } = new();
        public ObservableCollection<AppItem> RecentDocsCollection { get; } = new();
        public ObservableCollection<AppItem> AllAppsCollection { get; } = new();

        public ObservableCollection<AppItem> RecentlyAddedCollection { get; } = new();
        public ObservableCollection<AppItem> SuggestedAppsCollection { get; } = new();

        public ObservableCollection<AppItem> SearchResultsCollection { get; } = new();
        public ObservableCollection<AppItem> SearchAppsCollection { get; } = new();
        public ObservableCollection<AppItem> SearchSettingsCollection { get; } = new();

        public ObservableCollection<AppItem> SearchBestMatchCollection { get; } = new();
        public ObservableCollection<AppItem> SearchDocsCollection { get; } = new();
        public ObservableCollection<AppItem> SearchFilesCollection { get; } = new();

        public ObservableCollection<ShortcutItem> StartMenuShortcuts { get; } = new();

        public Visibility SuggestedVisibility => ShowSuggestedApps && SuggestedAppsCollection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RecentlyAddedVisibility => ShowRecentlyAdded && RecentlyAddedCollection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasAnyHeaderContent => (SuggestedVisibility == Visibility.Visible || RecentlyAddedVisibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BothHeadersVisible => (SuggestedVisibility == Visibility.Visible && RecentlyAddedVisibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;

        private bool _showSuggestedApps = SettingsEngine.Shell_StartMenuShowSuggested;
        public bool ShowSuggestedApps
        {
            get => _showSuggestedApps;
            set
            {
                if (SetProperty(ref _showSuggestedApps, value))
                {
                    SettingsEngine.Shell_StartMenuShowSuggested = value;

                    if (value && SuggestedAppsCollection.Count == 0)
                    {
                        UpdateSuggestedApps();
                    }

                    OnPropertyChanged(nameof(SuggestedVisibility));
                    OnPropertyChanged(nameof(HasAnyHeaderContent));
                    OnPropertyChanged(nameof(BothHeadersVisible));
                }
            }
        }

        private bool _showRecentlyAdded = SettingsEngine.Shell_StartMenuShowRecentlyAdded;
        public bool ShowRecentlyAdded
        {
            get => _showRecentlyAdded;
            set
            {
                if (SetProperty(ref _showRecentlyAdded, value))
                {
                    SettingsEngine.Shell_StartMenuShowRecentlyAdded = value;
                    OnPropertyChanged(nameof(RecentlyAddedVisibility));
                    OnPropertyChanged(nameof(HasAnyHeaderContent));
                    OnPropertyChanged(nameof(BothHeadersVisible));
                }
            }
        }

        public List<AppItem> AllRecentDocs { get; } = new();

        public bool IsDataLoaded { get; private set; } = false;

        private FileSystemWatcher? _userStartMenuWatcher;
        private FileSystemWatcher? _systemStartMenuWatcher;
        private DispatcherTimer? _appRefreshDebounceTimer;
        private CancellationTokenSource? _searchCts;

        public Action<string, ImageSource?, string, string>? OnUserProfileLoaded;
        public Action? OnAppsDataLoaded;
        #endregion

        #region Core Windows Utilities List
        private static readonly List<AppItem> KnownSystemApps = new List<AppItem>
        {
            new AppItem { Name = "Registry Editor", ExecutablePath = @"C:\Windows\regedit.exe", FallbackGlyph = "\xE74C", IsUwp = false, IconScale = 1.0 },
            new AppItem { Name = "Command Prompt", ExecutablePath = @"C:\Windows\System32\cmd.exe", FallbackGlyph = "\xE756", IsUwp = false, IconScale = 1.0 },
            new AppItem { Name = "Task Manager", ExecutablePath = @"C:\Windows\System32\Taskmgr.exe", FallbackGlyph = "\xE9F5", IsUwp = false, IconScale = 1.0 },
            new AppItem { Name = "Control Panel", ExecutablePath = @"C:\Windows\System32\control.exe", FallbackGlyph = "\xE713", IsUwp = false, IconScale = 1.0 },
            new AppItem { Name = "Calculator", ExecutablePath = @"C:\Windows\System32\calc.exe", FallbackGlyph = "\xE1D0", IsUwp = false, IconScale = 1.0 },
            new AppItem { Name = "Notepad", ExecutablePath = @"C:\Windows\notepad.exe", FallbackGlyph = "\xE70B", IsUwp = false, IconScale = 1.0 },
            new AppItem { Name = "File Explorer", ExecutablePath = @"C:\Windows\explorer.exe", FallbackGlyph = "\xE838", IsUwp = false, IconScale = 1.0 },
            new AppItem { Name = "Services", ExecutablePath = @"C:\Windows\System32\services.msc", FallbackGlyph = "\xE713", IsUwp = false, IconScale = 1.0 }
        };
        #endregion

        #region Initialization & Data Loading
        public void InitializeAppWatchers(Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
        {
            try
            {
                _appRefreshDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _appRefreshDebounceTimer.Tick += (s, e) =>
                {
                    _appRefreshDebounceTimer.Stop();
                    IsDataLoaded = false;
                    LoadAppsData(dispatcher);
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
                    _userStartMenuWatcher.Created += (s, e) => OnStartMenuChanged(e, dispatcher);
                    _userStartMenuWatcher.Deleted += (s, e) => OnStartMenuChanged(e, dispatcher);
                    _userStartMenuWatcher.Renamed += (s, e) => OnStartMenuChanged(e, dispatcher);
                }

                if (Directory.Exists(systemStartMenu))
                {
                    _systemStartMenuWatcher = new FileSystemWatcher(systemStartMenu)
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    };
                    _systemStartMenuWatcher.Created += (s, e) => OnStartMenuChanged(e, dispatcher);
                    _systemStartMenuWatcher.Deleted += (s, e) => OnStartMenuChanged(e, dispatcher);
                    _systemStartMenuWatcher.Renamed += (s, e) => OnStartMenuChanged(e, dispatcher);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize start menu watchers: {ex.Message}");
            }
        }

        private void OnStartMenuChanged(FileSystemEventArgs e, Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
        {
            if (e.FullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                e.FullPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(e.FullPath))
            {
                dispatcher.TryEnqueue(() =>
                {
                    _appRefreshDebounceTimer?.Stop();
                    _appRefreshDebounceTimer?.Start();
                });
            }
        }

        public async void LoadUserProfile(Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
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

                    var picStreamRef = await user.GetPictureAsync(UserPictureSize.Size424x424);
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

            string accountType = (!string.IsNullOrEmpty(userEmail) && userEmail.Contains("@")) ? "Microsoft Account" : "Local Account";

            dispatcher.TryEnqueue(() =>
            {
                OnUserProfileLoaded?.Invoke(displayName, profileImage, userEmail, accountType);
            });
        }

        public async void LoadAppsData(Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
        {
            if (IsDataLoaded) return;
            IsDataLoaded = true;

            try
            {
                var fetchedAllApps = await StartMenuHelper.GetAllAppsAsync();

                if (fetchedAllApps.Count > 0)
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        AllAppsCollection.Clear();
                        RecentlyAddedCollection.Clear();

                        foreach (var app in fetchedAllApps)
                        {
                            try
                            {
                                if (app.IsUwp)
                                {
                                    if (!string.IsNullOrEmpty(app.ExecutablePath))
                                    {
                                        string? dirPath = Path.GetDirectoryName(app.ExecutablePath);

                                        if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
                                        {
                                            app.InstallDate = Directory.GetCreationTime(dirPath);
                                            app.IsNew = app.InstallDate > DateTime.Now.AddDays(-7);
                                        }
                                    }
                                }
                                else if (!string.IsNullOrEmpty(app.ExecutablePath) && File.Exists(app.ExecutablePath))
                                {
                                    app.InstallDate = File.GetCreationTime(app.ExecutablePath);
                                    app.IsNew = app.InstallDate > DateTime.Now.AddDays(-7);
                                }
                            }
                            catch { }

                            AllAppsCollection.Add(app);

                            if (app.IsNew)
                            {
                                RecentlyAddedCollection.Add(app);
                            }
                        }

                        var sortedRecent = RecentlyAddedCollection.OrderByDescending(a => a.InstallDate).ToList();
                        RecentlyAddedCollection.Clear();
                        foreach (var app in sortedRecent)
                        {
                            RecentlyAddedCollection.Add(app);
                        }

                        Pages.Clear();

                        string savedPins = SettingsEngine.StartMenuPinnedApps;
                        if (!string.IsNullOrWhiteSpace(savedPins))
                        {
                            var pageChunks = savedPins.Split(new[] { "---SUPERPAGE---" }, StringSplitOptions.RemoveEmptyEntries);

                            foreach (var pageChunk in pageChunks)
                            {
                                var page = new StartMenuPage { PageIndex = Pages.Count };
                                var categories = pageChunk.Split(new[] { "---PAGE---" }, StringSplitOptions.RemoveEmptyEntries);

                                foreach (var catStr in categories)
                                {
                                    var parts = catStr.Split(new[] { '|' }, 2);
                                    if (parts.Length == 2)
                                    {
                                        string groupName = parts[0];

                                        if (groupName.StartsWith("[TABBED_GROUP]"))
                                        {
                                            var cat = new AppCategory
                                            {
                                                Name = groupName.Substring(14),
                                                IsTabbed = true,
                                                Tabs = new ObservableCollection<AppCategory>()
                                            };

                                            var tabStrings = parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries);
                                            foreach (var tabStr in tabStrings)
                                            {
                                                var tabParts = tabStr.Split(new[] { ':' }, 2);
                                                if (tabParts.Length >= 1)
                                                {
                                                    string rawName = tabParts[0];
                                                    string extractedColor = "";
                                                    int colorStart = rawName.LastIndexOf('[');

                                                    if (colorStart != -1 && rawName.EndsWith("]"))
                                                    {
                                                        extractedColor = rawName.Substring(colorStart + 1, rawName.Length - colorStart - 2);
                                                        rawName = rawName.Substring(0, colorStart);
                                                    }

                                                    var newTab = new AppCategory
                                                    {
                                                        Name = rawName,
                                                        TabColor = (extractedColor == "NONE" || string.IsNullOrEmpty(extractedColor)) ? null : extractedColor
                                                    };

                                                    if (tabParts.Length == 2)
                                                    {
                                                        var pinNames = tabParts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                                                        PopulateApps(newTab, pinNames, fetchedAllApps);
                                                    }
                                                    cat.Tabs.Add(newTab);
                                                }
                                            }
                                            page.PinnedCategories.Add(cat);
                                        }
                                        else
                                        {
                                            var cat = new AppCategory { Name = groupName };
                                            var pinNames = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                                            PopulateApps(cat, pinNames, fetchedAllApps);
                                            page.PinnedCategories.Add(cat);
                                        }
                                    }
                                }

                                if (page.PinnedCategories.Count == 0)
                                {
                                    page.PinnedCategories.Add(new AppCategory { Name = "New Section" });
                                }
                                Pages.Add(page);
                            }
                        }

                        if (Pages.Count == 0)
                        {
                            var defaultPage = new StartMenuPage { PageIndex = 0 };
                            var defaultCat = new AppCategory { Name = "Pinned" };
                            var preferredPins = new[] { "Edge", "Settings", "File Explorer", "Store", "Photos", "Camera", "Calculator", "Clock", "Terminal", "Spotify", "Discord", "Word", "Excel" };
                            var defaultPinned = fetchedAllApps.Where(a => preferredPins.Any(p => a.Name != null && a.Name.Contains(p, StringComparison.OrdinalIgnoreCase))).Take(12).ToList();

                            foreach (var app in defaultPinned) defaultCat.Apps.Add(app);
                            defaultPage.PinnedCategories.Add(defaultCat);
                            Pages.Add(defaultPage);
                        }

                        UpdateSuggestedApps();

                        OnAppsDataLoaded?.Invoke();

                        var allCategories = Pages.SelectMany(p => p.PinnedCategories)
                            .SelectMany(c => c.IsTabbed ? (IEnumerable<AppCategory>)c.Tabs : new AppCategory[] { c })
                            .ToList();

                        var topLevelPinnedApps = allCategories.SelectMany(cat => cat.Apps).ToList();

                        var nestedFolderApps = topLevelPinnedApps
                            .Where(app => app.FolderApps != null)
                            .SelectMany(app => app.FolderApps)
                            .ToList();

                        var allPinnedAndFolderApps = topLevelPinnedApps.Concat(nestedFolderApps).ToList();

                        _ = ExtractIconsAsync(allPinnedAndFolderApps);
                        _ = ExtractIconsAsync(AllAppsCollection);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load apps: {ex.Message}");
            }
        }

        private void PopulateApps(AppCategory cat, string[] pinNames, List<AppItem> fetchedAllApps)
        {
            foreach (var pinString in pinNames)
            {
                if (pinString.StartsWith("[PINNED_FOLDER]"))
                {
                    var separatorIndex = pinString.IndexOf("::");
                    string folderName = "Map";
                    string contents = "";

                    if (separatorIndex != -1)
                    {
                        folderName = pinString.Substring(15, separatorIndex - 15);
                        contents = pinString.Substring(separatorIndex + 2);
                    }

                    var folderItem = new AppItem
                    {
                        Name = string.IsNullOrWhiteSpace(folderName) ? "Map" : folderName,
                        ExecutablePath = "PINNED_FOLDER",
                        FallbackGlyph = "",
                        IsUwp = false
                    };

                    if (!string.IsNullOrEmpty(contents))
                    {
                        var innerApps = contents.Split('~', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var innerAppStr in innerApps)
                        {
                            string innerName = innerAppStr;
                            string innerPath = "";
                            string innerImg = "";
                            string innerTint = "";

                            var pipeParts = innerAppStr.Split('|');
                            if (pipeParts.Length >= 1) innerName = pipeParts[0];
                            if (pipeParts.Length >= 2) innerPath = pipeParts[1];
                            if (pipeParts.Length >= 3) innerImg = pipeParts[2] == "NONE" ? "" : pipeParts[2];
                            if (pipeParts.Length >= 4) innerTint = pipeParts[3] == "NONE" ? "" : pipeParts[3];

                            var innerMatch = fetchedAllApps.FirstOrDefault(a => !string.IsNullOrEmpty(innerPath) && a.ExecutablePath == innerPath)
                                             ?? fetchedAllApps.FirstOrDefault(a => a.Name == innerName);

                            if (innerMatch != null)
                            {
                                if (!string.IsNullOrEmpty(innerImg))
                                {
                                    innerMatch.CustomImagePath = innerImg;
                                    innerMatch.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(innerImg));
                                }
                                if (!string.IsNullOrEmpty(innerTint)) innerMatch.TintColor = innerTint;

                                folderItem.FolderApps.Add(innerMatch);
                            }
                            else if (!string.IsNullOrEmpty(innerPath))
                            {
                                var fallbackApp = new AppItem
                                {
                                    Name = innerName,
                                    ExecutablePath = innerPath,
                                    IsUwp = false,
                                    FallbackGlyph = "\xE738",
                                    IconScale = 1.0
                                };

                                if (!string.IsNullOrEmpty(innerImg))
                                {
                                    fallbackApp.CustomImagePath = innerImg;
                                    fallbackApp.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(innerImg));
                                }
                                if (!string.IsNullOrEmpty(innerTint)) fallbackApp.TintColor = innerTint;

                                folderItem.FolderApps.Add(fallbackApp);
                            }
                        }
                    }
                    cat.Apps.Add(folderItem);
                }
                else
                {
                    string appName = pinString;
                    string customImg = "";
                    string customTint = "";

                    var parts = pinString.Split('|');
                    if (parts.Length >= 1) appName = parts[0];
                    if (parts.Length >= 2) customImg = parts[1] == "NONE" ? "" : parts[1];
                    if (parts.Length >= 3) customTint = parts[2] == "NONE" ? "" : parts[2];

                    var match = fetchedAllApps.FirstOrDefault(a => a.Name == appName);
                    if (match != null)
                    {
                        if (!string.IsNullOrEmpty(customImg))
                        {
                            match.CustomImagePath = customImg;
                            match.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(customImg));
                        }
                        if (!string.IsNullOrEmpty(customTint))
                        {
                            match.TintColor = customTint;
                        }

                        cat.Apps.Add(match);
                    }
                }
            }
        }

        public void UpdateSuggestedApps()
        {
            SuggestedAppsCollection.Clear();

            if (!ShowSuggestedApps || AllAppsCollection == null || AllAppsCollection.Count == 0)
                return;

            var userLaunchCounts = GetUserAssistRunCounts();

            var rankedApps = AllAppsCollection
                .Select(app => new
                {
                    App = app,
                    Count = userLaunchCounts.Where(kvp =>
                        app.ExecutablePath != null &&
                        (kvp.Key.Equals(app.ExecutablePath, StringComparison.OrdinalIgnoreCase) ||
                         kvp.Key.EndsWith(Path.GetFileName(app.ExecutablePath), StringComparison.OrdinalIgnoreCase)))
                        .Select(kvp => kvp.Value)
                        .FirstOrDefault()
                })
                .Where(x => x.Count > 0 && !x.App.IsNew && x.App.ExecutablePath != "PINNED_FOLDER")
                .OrderByDescending(x => x.Count)
                .Select(x => x.App)
                .Distinct()
                .Take(4)
                .ToList();

            if (rankedApps.Count < 4)
            {
                var fallbackApps = AllAppsCollection
                    .Where(a => !a.IsNew && !rankedApps.Contains(a) && a.ExecutablePath != "PINNED_FOLDER")
                    .OrderBy(x => Guid.NewGuid())
                    .Take(4 - rankedApps.Count);

                rankedApps.AddRange(fallbackApps);
            }

            foreach (var app in rankedApps)
            {
                SuggestedAppsCollection.Add(app);
            }

            OnPropertyChanged(nameof(SuggestedVisibility));
            OnPropertyChanged(nameof(HasAnyHeaderContent));
            OnPropertyChanged(nameof(BothHeadersVisible));
        }

        #region Authentic Windows UserAssist Engine

        private Dictionary<string, int> GetUserAssistRunCounts()
        {
            var runCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            string[] userAssistKeys = new[]
            {
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist\{CEBFF5CD-ACE2-4F4F-9178-9926F41749EA}\Count",
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist\{F4E57C4B-2036-45F0-A9AB-443BCFE33D9F}\Count",
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist\{B267E3AD-A825-4A09-82B9-EEC22AA3B847}\Count"
            };

            using (var hklm = Microsoft.Win32.Registry.CurrentUser)
            {
                foreach (var subKeyPath in userAssistKeys)
                {
                    using (var key = hklm.OpenSubKey(subKeyPath))
                    {
                        if (key == null) continue;

                        foreach (var valueName in key.GetValueNames())
                        {
                            var data = key.GetValue(valueName) as byte[];

                            if (data != null && data.Length >= 8)
                            {
                                int count = BitConverter.ToInt32(data, 4);
                                if (count > 0)
                                {
                                    string decodedPath = DecodeROT13(valueName);

                                    if (decodedPath.StartsWith("P~")) decodedPath = decodedPath.Substring(2);
                                    if (decodedPath.Contains("UEME_RUNPATH:")) decodedPath = decodedPath.Replace("UEME_RUNPATH:", "");
                                    if (decodedPath.Contains("UEME_CTLSESSION:")) continue;

                                    if (runCounts.ContainsKey(decodedPath))
                                        runCounts[decodedPath] += count;
                                    else
                                        runCounts[decodedPath] = count;
                                }
                            }
                        }
                    }
                }
            }
            return runCounts;
        }

        private static string DecodeROT13(string input)
        {
            char[] array = input.ToCharArray();
            for (int i = 0; i < array.Length; i++)
            {
                int c = array[i];
                if (c >= 'a' && c <= 'z')
                    array[i] = (char)(c + 13 > 'z' ? c - 13 : c + 13);
                else if (c >= 'A' && c <= 'Z')
                    array[i] = (char)(c + 13 > 'Z' ? c - 13 : c + 13);
            }
            return new string(array);
        }

        #endregion

        public async Task ExtractIconsAsync(IEnumerable<AppItem> apps)
        {
            foreach (var app in apps)
            {
                if (app.IconSource == null && app.ExecutablePath != "PINNED_FOLDER")
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

        public void LoadRecentDocuments(bool showRecentDocs)
        {
            if (Pages.Count == 0) return;
            var firstPage = Pages[0];
            firstPage.RecentDocsCollection.Clear();
            AllRecentDocs.Clear();

            if (!showRecentDocs)
            {
                firstPage.RecentDocsVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                return;
            }

            try
            {
                string recentPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                if (Directory.Exists(recentPath))
                {
                    var recentFiles = new DirectoryInfo(recentPath).GetFiles("*.lnk")
                        .OrderByDescending(f => f.LastWriteTime)
                        .Take(24);

                    foreach (var file in recentFiles)
                    {
                        AllRecentDocs.Add(new AppItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file.Name),
                            ExecutablePath = file.FullName,
                            FallbackGlyph = "\xE8A5",
                            IsUwp = false
                        });
                    }
                    _ = ExtractIconsAsync(AllRecentDocs);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Recent Docs Error: {ex.Message}"); }
        }

        public void UpdateShortcuts(string payload)
        {
            StartMenuShortcuts.Clear();
            if (string.IsNullOrWhiteSpace(payload)) return;

            foreach (var itemStr in payload.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = itemStr.Split('|');
                if (parts.Length >= 4)
                {
                    int displayMode = int.TryParse(parts[2], out int mode) ? mode : 0;
                    if (displayMode == 2) continue;

                    string displayName = GetLocalizedName(parts[0]);

                    StartMenuShortcuts.Add(new ShortcutItem
                    {
                        Name = displayName,
                        TargetPath = parts[1],
                        DisplayModeIndex = displayMode,
                        IsSeparator = parts[3] == "1",
                        IconGlyph = parts.Length > 4 && !string.IsNullOrEmpty(parts[4]) ? parts[4] : GetDefaultGlyph(parts[1]),
                        IconImagePath = parts.Length > 5 ? parts[5] : string.Empty
                    });
                }
            }
        }

        private string GetDefaultGlyph(string targetPath)
        {
            if (targetPath.Contains("Documents")) return "\xE8A5";
            if (targetPath.Contains("Downloads")) return "\xE896";
            if (targetPath.Contains("Music")) return "\xE8D6";
            if (targetPath.Contains("Pictures")) return "\xE8B9";
            if (targetPath.Contains("Settings") || targetPath.Contains("ms-settings")) return "\xE713";
            if (targetPath.Contains("Run")) return "\xE78B";
            if (targetPath.Contains("control.exe")) return "\xE713";
            return "\xE8B7";
        }

        private string GetLocalizedName(string rawName)
        {
            string? resourceKey = rawName switch
            {
                "Documents" => "StartMenu_FolderDocuments",
                "Downloads" => "StartMenu_FolderDownloads",
                "Music" => "StartMenu_FolderMusic",
                "Pictures" => "StartMenu_FolderPictures",
                "Settings" => "StartMenu_FolderSettings",
                "Run" => "StartMenu_FolderRun",
                _ => null
            };

            if (resourceKey != null)
            {
                string localizedString = LocalizationService.Instance.GetString(resourceKey);
                if (!string.IsNullOrEmpty(localizedString)) return localizedString;
            }
            return rawName;
        }
        #endregion

        #region Context Menu Handlers (Pinning / Actions)
        public void SaveStartMenuPins()
        {
            var pageStrings = new List<string>();
            foreach (var page in Pages)
            {
                var categoryStrings = new List<string>();
                foreach (var cat in page.PinnedCategories)
                {
                    if (cat.IsTabbed && cat.Tabs != null)
                    {
                        string baseName = string.IsNullOrWhiteSpace(cat.Name) ? "Tabbed Group" : cat.Name;
                        var tabStrings = new List<string>();

                        foreach (var tab in cat.Tabs)
                        {
                            string tabName = string.IsNullOrWhiteSpace(tab.Name) ? "Tab" : tab.Name;
                            string tabColor = string.IsNullOrWhiteSpace(tab.TabColor) ? "NONE" : tab.TabColor;
                            var tabApps = SerializeAppList(tab.Apps);

                            tabStrings.Add($"{tabName}[{tabColor}]:{string.Join(",", tabApps)}");
                        }
                        categoryStrings.Add($"[TABBED_GROUP]{baseName}|{string.Join(";", tabStrings)}");
                    }
                    else
                    {
                        var appStrings = SerializeAppList(cat.Apps);
                        categoryStrings.Add($"{cat.Name}|{string.Join(",", appStrings)}");
                    }
                }
                pageStrings.Add(string.Join("---PAGE---", categoryStrings));
            }
            SettingsEngine.StartMenuPinnedApps = string.Join("---SUPERPAGE---", pageStrings);
        }

        private List<string> SerializeAppList(IEnumerable<AppItem> apps)
        {
            var appStrings = new List<string>();
            foreach (var app in apps)
            {
                if (app == null) continue;

                if (app.ExecutablePath == "PINNED_FOLDER")
                {
                    var validApps = app.FolderApps?
                        .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.ExecutablePath))
                        .Select(a => {
                            string cImg = string.IsNullOrEmpty(a.CustomImagePath) ? "NONE" : a.CustomImagePath;
                            string cTint = string.IsNullOrEmpty(a.TintColor) ? "NONE" : a.TintColor;
                            return $"{a.Name}|{a.ExecutablePath}|{cImg}|{cTint}";
                        }) ?? new List<string>();

                    var insideApps = string.Join("~", validApps);
                    appStrings.Add($"[PINNED_FOLDER]{app.Name}::{insideApps}");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(app.Name))
                    {
                        string cImg = string.IsNullOrEmpty(app.CustomImagePath) ? "NONE" : app.CustomImagePath;
                        string cTint = string.IsNullOrEmpty(app.TintColor) ? "NONE" : app.TintColor;

                        appStrings.Add($"{app.Name}|{cImg}|{cTint}");
                    }
                }
            }
            return appStrings;
        }

        private string GetTaskbarFolderPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

        public bool IsPinnedToTaskbar(AppItem app)
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

        public void ToggleTaskbarPin(AppItem app, bool isPinned)
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

                        string realTarget = app.ExecutablePath;
                        if (realTarget.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                        {
                            string parsed = StartMenuHelper.ParseShortcutTarget(realTarget);
                            if (!string.IsNullOrEmpty(parsed) && File.Exists(parsed))
                            {
                                realTarget = parsed;
                            }
                        }

                        shortcut.TargetPath = realTarget;
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

        #region Search Logic

        public void PerformSearch(string query, string currentSearchFilter, Microsoft.UI.Dispatching.DispatcherQueue dispatcher, Action<AppItem> onFileFound)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            SearchResultsCollection.Clear();
            SearchAppsCollection.Clear();
            SearchSettingsCollection.Clear();
            SearchBestMatchCollection.Clear();
            SearchDocsCollection.Clear();
            SearchFilesCollection.Clear();

            if (string.IsNullOrWhiteSpace(query)) return;

            bool searchApps = currentSearchFilter == "Apps" || currentSearchFilter == "All";
            bool searchFiles = currentSearchFilter == "Apps" || currentSearchFilter == "Files" || currentSearchFilter == "All";

            if (searchApps)
            {
                var combinedApps = AllAppsCollection
                    .Concat(KnownSystemApps)
                    .GroupBy(a => a.ExecutablePath ?? a.Name)
                    .Select(g => g.First());

                var appResults = combinedApps
                    .Where(a => a.Name != null &&
                                (a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                (a.ExecutablePath != null && Path.GetFileNameWithoutExtension(a.ExecutablePath).Contains(query, StringComparison.OrdinalIgnoreCase))) &&
                                a.ExecutablePath != "PINNED_FOLDER" &&
                                a.FallbackGlyph != "\xE8B7" &&
                                (string.IsNullOrEmpty(a.ExecutablePath) || !Directory.Exists(a.ExecutablePath)))
                    .OrderByDescending(a => a.Name!.Equals(query, StringComparison.OrdinalIgnoreCase) || (a.ExecutablePath != null && Path.GetFileNameWithoutExtension(a.ExecutablePath).Equals(query, StringComparison.OrdinalIgnoreCase))) // Priority 1: Exact Match
                    .ThenByDescending(a => a.Name!.StartsWith(query, StringComparison.OrdinalIgnoreCase) || (a.ExecutablePath != null && Path.GetFileNameWithoutExtension(a.ExecutablePath).StartsWith(query, StringComparison.OrdinalIgnoreCase))) // Priority 2: Starts With
                    .ThenByDescending(a => !string.IsNullOrEmpty(a.ExecutablePath) &&
                                           (a.ExecutablePath.Contains("regedit", StringComparison.OrdinalIgnoreCase) ||
                                            a.ExecutablePath.Contains("System32", StringComparison.OrdinalIgnoreCase) ||
                                            a.ExecutablePath.Contains("Windows Tools", StringComparison.OrdinalIgnoreCase) ||
                                            a.ExecutablePath.Contains("Administrative Tools", StringComparison.OrdinalIgnoreCase))) // Priority 3: Boost OS Tools
                    .ThenBy(a => a.Name) // Priority 4: Alphabetical
                    .ToList();

                _ = ExtractIconsAsync(appResults);

                var settingsResults = Helpers.SettingsProvider.KnownSettings
                    .Where(s => s.Name != null && s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(s => s.Name!.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    .ThenBy(s => s.Name)
                    .ToList();

                foreach (var item in appResults)
                {
                    SearchResultsCollection.Add(item);
                    SearchAppsCollection.Add(item);
                }

                foreach (var setting in settingsResults)
                {
                    SearchResultsCollection.Add(setting);
                    SearchSettingsCollection.Add(setting);
                }

                if (appResults.Count > 0)
                {
                    SearchBestMatchCollection.Add(appResults.First());
                }
                else if (settingsResults.Count > 0)
                {
                    SearchBestMatchCollection.Add(settingsResults.First());
                }
            }

            if (searchFiles)
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

                    searchPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                    searchPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                    searchPaths.Add(Environment.GetFolderPath(Environment.SpecialFolder.Recent));

                    string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    searchPaths.Add(Path.Combine(userPath, "Downloads"));

                    int resultsFound = 0;
                    const int maxResults = 15;

                    foreach (var path in searchPaths.Distinct())
                    {
                        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) continue;
                        if (token.IsCancellationRequested || resultsFound >= maxResults) break;

                        foreach (var file in SafeEnumerateFiles(path, query, token))
                        {
                            if (token.IsCancellationRequested || resultsFound >= maxResults) break;

                            try
                            {
                                var attrs = File.GetAttributes(file);
                                if (attrs.HasFlag(FileAttributes.Hidden) || attrs.HasFlag(FileAttributes.System)) continue;
                            }
                            catch { }

                            resultsFound++;

                            dispatcher.TryEnqueue(() =>
                            {
                                var fileItem = new AppItem
                                {
                                    Name = Path.GetFileNameWithoutExtension(file),
                                    ExecutablePath = file,
                                    FallbackGlyph = "\xE8A5",
                                    IsUwp = false,
                                    IconScale = 1.0
                                };

                                int insertIndex = SearchResultsCollection.Count > 0 ? SearchResultsCollection.Count - 1 : 0;
                                SearchResultsCollection.Insert(insertIndex, fileItem);

                                string ext = Path.GetExtension(file).ToLowerInvariant();

                                if (ext == ".doc" || ext == ".docx" || ext == ".pdf" || ext == ".txt" || ext == ".md" || ext == ".rtf" || ext == ".csv" || ext == ".xlsx" || ext == ".lnk")
                                {
                                    SearchDocsCollection.Add(fileItem);
                                }
                                else
                                {
                                    SearchFilesCollection.Add(fileItem);
                                }

                                if (SearchBestMatchCollection.Count == 0)
                                {
                                    SearchBestMatchCollection.Add(fileItem);
                                }

                                _ = ExtractIconsAsync(new[] { fileItem });
                                onFileFound?.Invoke(fileItem);
                            });
                        }
                    }
                }, token);
            }
            else if (currentSearchFilter == "Web")
            {
                SearchResultsCollection.Add(new AppItem
                {
                    Name = LocalizationService.Instance.GetString("StartMenu_SearchWebPrefix", query),
                    FallbackGlyph = "\xE8FA",
                    ExecutablePath = "WEB_SEARCH:" + query
                });
            }
        }

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
        #endregion

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
        protected bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}