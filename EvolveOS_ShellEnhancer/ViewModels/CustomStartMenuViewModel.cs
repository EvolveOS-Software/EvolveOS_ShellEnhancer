// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using Windows.System;

namespace EvolveOS_ShellEnhancer.ViewModels
{
    public class CustomStartMenuViewModel
    {
        #region Fields & Properties
        public ObservableCollection<StartMenuPage> Pages { get; } = new();
        public ObservableCollection<AppCategory> PinnedCategories { get; } = new();
        public ObservableCollection<AppItem> RecentDocsCollection { get; } = new();
        public ObservableCollection<AppItem> AllAppsCollection { get; } = new();
        public ObservableCollection<AppItem> SearchResultsCollection { get; } = new();
        public ObservableCollection<ShortcutItem> StartMenuShortcuts { get; } = new();

        public List<AppItem> AllRecentDocs { get; } = new();

        public bool IsDataLoaded { get; private set; } = false;

        private FileSystemWatcher? _userStartMenuWatcher;
        private FileSystemWatcher? _systemStartMenuWatcher;
        private DispatcherTimer? _appRefreshDebounceTimer;
        private CancellationTokenSource? _searchCts;

        public Action<string, ImageSource?, string, string>? OnUserProfileLoaded;
        public Action? OnAppsDataLoaded;
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
                        foreach (var app in fetchedAllApps) AllAppsCollection.Add(app);

                        Pages.Clear();

                        string savedPins = SettingsEngine.StartMenuPinnedApps;
                        if (!string.IsNullOrWhiteSpace(savedPins))
                        {
                            var pageChunks = savedPins.Split(new[] { "---PAGE---" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var pageChunk in pageChunks)
                            {
                                var page = new StartMenuPage { PageIndex = Pages.Count };
                                var categories = pageChunk.Split(';', StringSplitOptions.RemoveEmptyEntries);

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
                                        page.PinnedCategories.Add(cat);
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

                        OnAppsDataLoaded?.Invoke();

                        _ = ExtractIconsAsync(Pages.SelectMany(p => p.PinnedCategories).SelectMany(c => c.Apps).ToList());
                        _ = ExtractIconsAsync(AllAppsCollection);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load apps: {ex.Message}");
            }
        }

        public async Task ExtractIconsAsync(IEnumerable<AppItem> apps)
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
                    var appNames = cat.Apps.Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n));
                    categoryStrings.Add($"{cat.Name}|{string.Join(",", appNames)}");
                }
                pageStrings.Add(string.Join(";", categoryStrings));
            }
            SettingsEngine.StartMenuPinnedApps = string.Join("---PAGE---", pageStrings);
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
            if (string.IsNullOrWhiteSpace(query)) return;

            if (currentSearchFilter == "Apps")
            {
                var results = AllAppsCollection
                    .Where(a => a.Name != null && a.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var item in results) SearchResultsCollection.Add(item);
            }
            else if (currentSearchFilter == "Files")
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

                            dispatcher.TryEnqueue(() =>
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
    }
}