// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    #region AppItem Model
    [Bindable]
    public class AppItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string? Name { get; set; }
        public string? ExecutablePath { get; set; }
        public string? FallbackGlyph { get; set; }
        public bool IsUwp { get; set; }

        public double IconScale { get; set; } = 1.0;

        private ImageSource? _iconSource;
        public ImageSource? IconSource
        {
            get => _iconSource;
            set
            {
                if (_iconSource != value)
                {
                    _iconSource = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IconSource)));
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasIcon)));
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasNoIcon)));
                }
            }
        }

        internal IRandomAccessStreamReference? UwpLogoStreamRef { get; set; }

        public Visibility HasIcon => IconSource != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasNoIcon => IconSource == null ? Visibility.Visible : Visibility.Collapsed;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    #endregion

    public static class StartMenuHelper
    {
        #region Constants & Exclusion Lists
        private static readonly string[] JunkKeywords = new[]
        {
            "uninstall", "setup", "update", "readme", "help", "manual",
            "documentation", "website", "visit", "license", "support", "pdf", "html", "install"
        };

        // Add keywords for any UWP app that already has a giant/full-bleed icon.
        // This stops them from being scaled up like the rest of the Windows apps.
        private static readonly string[] UnpaddedUwpKeywords = new[]
        {
            "dts", "optimizer", "evolveos", "realtek", "windbg"
        };
        #endregion

        #region App Enumeration
        public static async Task<List<AppItem>> GetAllAppsAsync()
        {
            var apps = new List<AppItem>();
            var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var pkgManager = new PackageManager();
                var packages = pkgManager.FindPackagesForUser(string.Empty);

                foreach (var pkg in packages)
                {
                    if (pkg.IsFramework || pkg.IsResourcePackage) continue;

                    try
                    {
                        var appEntries = await pkg.GetAppListEntriesAsync();
                        foreach (var entry in appEntries)
                        {
                            string name = entry.DisplayInfo.DisplayName;
                            if (string.IsNullOrWhiteSpace(name) || !uniqueNames.Add(name)) continue;

                            bool isUnpadded = UnpaddedUwpKeywords.Any(keyword =>
                                name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                entry.AppUserModelId.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                            apps.Add(new AppItem
                            {
                                Name = name,
                                ExecutablePath = entry.AppUserModelId,
                                IsUwp = true,
                                FallbackGlyph = "\xE713",
                                UwpLogoStreamRef = entry.DisplayInfo.GetLogo(new Windows.Foundation.Size(256, 256)),
                                // Default all UWP apps to 3.0x to fix the padding, UNLESS they are in the exclusion list!
                                IconScale = isUnpadded ? 1.0 : 3.0
                            });
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UWP Enumeration Error: {ex.Message}");
            }

            var paths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs)
            };

            foreach (var path in paths)
            {
                if (!Directory.Exists(path)) continue;

                try
                {
                    var shortcutFiles = Directory.GetFiles(path, "*.lnk", SearchOption.TopDirectoryOnly)
                        .Concat(Directory.GetDirectories(path).SelectMany(subDir => Directory.GetFiles(subDir, "*.lnk", SearchOption.TopDirectoryOnly)));

                    foreach (var file in shortcutFiles)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);

                        if (JunkKeywords.Any(junk => name.Contains(junk, StringComparison.OrdinalIgnoreCase))) continue;

                        string target = ParseShortcutTarget(file);
                        if (!string.IsNullOrEmpty(target) && target.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                        if (uniqueNames.Add(name))
                        {
                            apps.Add(new AppItem
                            {
                                Name = name,
                                ExecutablePath = file,
                                IsUwp = false,
                                FallbackGlyph = "\xE738",
                                IconScale = 1.0
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Win32 Enumeration Error: {ex.Message}");
                }
            }

            if (uniqueNames.Add("File Explorer"))
            {
                apps.Add(new AppItem
                {
                    Name = "File Explorer",
                    ExecutablePath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\explorer.exe"),
                    IsUwp = false,
                    FallbackGlyph = "\xE838",
                    IconScale = 1.0
                });
            }

            return apps.OrderBy(a => a.Name).ToList();
        }

        public static async Task<List<AppItem>> GetPinnedAppsAsync()
        {
            var allApps = await GetAllAppsAsync();
            var preferredPins = new[] { "Edge", "Settings", "File Explorer", "Store", "Photos", "Mail", "Calculator", "Notepad", "Terminal", "Spotify", "Discord", "Word", "Excel" };

            var pinned = allApps.Where(a => preferredPins.Any(p => a.Name != null && a.Name.Contains(p, StringComparison.OrdinalIgnoreCase))).ToList();

            foreach (var app in allApps)
            {
                if (pinned.Count >= 12) break;
                if (!pinned.Contains(app)) pinned.Add(app);
            }

            return pinned.Take(12).ToList();
        }
        #endregion

        #region Shortcut Parsing
        public static string ParseShortcutTarget(string lnkPath)
        {
            try
            {
                IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(lnkPath);
                return shortcut.TargetPath?.Trim().Trim('"', '\'') ?? string.Empty;
            }
            catch { return string.Empty; }
        }
        #endregion

        #region Icon Extraction
        public static async Task<ImageSource?> ExtractAppIconAsync(AppItem appItem)
        {
            try
            {
                if (string.IsNullOrEmpty(appItem.ExecutablePath)) return null;

                if (appItem.IsUwp && appItem.UwpLogoStreamRef != null)
                {
                    using IRandomAccessStreamWithContentType stream = await appItem.UwpLogoStreamRef.OpenReadAsync();
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);
                    return bitmap;
                }

                string target = appItem.ExecutablePath;
                if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        StorageFile lnkFile = await StorageFile.GetFileFromPathAsync(target);
                        var lnkThumbnail = await lnkFile.GetThumbnailAsync(ThumbnailMode.SingleItem, 48);
                        if (lnkThumbnail == null) lnkThumbnail = await lnkFile.GetThumbnailAsync(ThumbnailMode.ListView, 48);

                        if (lnkThumbnail != null)
                        {
                            var bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(lnkThumbnail);
                            return bitmapImage;
                        }
                    }
                    catch { }

                    string parsed = ParseShortcutTarget(target);
                    if (!string.IsNullOrEmpty(parsed) && File.Exists(parsed)) target = parsed;
                }

                if (File.Exists(target))
                {
                    try
                    {
                        StorageFile file = await StorageFile.GetFileFromPathAsync(target);
                        var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 48);
                        if (thumbnail == null) thumbnail = await file.GetThumbnailAsync(ThumbnailMode.ListView, 48);

                        if (thumbnail != null)
                        {
                            var bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(thumbnail);
                            return bitmapImage;
                        }
                    }
                    catch { }

                    using var icon = Icon.ExtractAssociatedIcon(target);
                    if (icon != null)
                    {
                        using var bmp = icon.ToBitmap();
                        using var ms = new MemoryStream();
                        bmp.Save(ms, ImageFormat.Png);
                        ms.Position = 0;

                        var ras = new InMemoryRandomAccessStream();
                        using (var writer = new DataWriter(ras.GetOutputStreamAt(0)))
                        {
                            writer.WriteBytes(ms.ToArray());
                            await writer.StoreAsync();
                        }

                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(ras);
                        return bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Icon extraction failed for {appItem.Name}: {ex.Message}");
            }
            return null;
        }
        #endregion
    }
}