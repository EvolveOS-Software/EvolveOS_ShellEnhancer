// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    public static class StartMenuHelper
    {
        #region Fields & Properties
        private static readonly Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region Constants & Exclusion Lists
        private static readonly string[] JunkKeywords = new[]
        {
            "uninstall", "readme", "manual", "documentation", "license"
        };

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
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                            if (string.IsNullOrWhiteSpace(name))
                            {
                                name = pkg.Id.Name.Replace("Microsoft.Windows", "").Replace("Microsoft.", "");
                            }

                            if (string.IsNullOrWhiteSpace(name) || !uniqueNames.Add(name) || !uniquePaths.Add(entry.AppUserModelId)) continue;

                            bool isUnpadded = UnpaddedUwpKeywords.Any(keyword =>
                                name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                entry.AppUserModelId.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                            DateTime exactInstallDate = pkg.InstalledDate.LocalDateTime;

                            apps.Add(new AppItem
                            {
                                Name = name,
                                ExecutablePath = entry.AppUserModelId,
                                IsUwp = true,
                                FallbackGlyph = "\xE713",
                                UwpLogoStreamRef = entry.DisplayInfo.GetLogo(new Windows.Foundation.Size(256, 256)),
                                IconScale = isUnpadded ? 1.0 : 3.0,
                                InstallDate = exactInstallDate,
                                IsNew = exactInstallDate > DateTime.Now.AddDays(-7)
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
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
            };

            foreach (var basePath in paths)
            {
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath)) continue;

                try
                {
                    var rootFiles = Directory.GetFiles(basePath, "*.lnk")
                        .Concat(Directory.GetFiles(basePath, "*.url"))
                        .Concat(Directory.GetFiles(basePath, "*.appref-ms"));

                    foreach (var file in rootFiles)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);

                        var lowerName = name.ToLowerInvariant();
                        if (JunkKeywords.Any(junk => lowerName.Contains(junk)) || lowerName.EndsWith(" help") || lowerName.StartsWith("visit ")) continue;

                        string target = ParseShortcutTarget(file, out string args);
                        if (string.IsNullOrEmpty(target) || target.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                        string dedupeKey = target.ToLowerInvariant();
                        if (dedupeKey.EndsWith("explorer.exe") || dedupeKey.EndsWith("cmd.exe") || dedupeKey.EndsWith("rundll32.exe"))
                        {
                            dedupeKey += " " + args.ToLowerInvariant();
                        }

                        if (uniquePaths.Add(dedupeKey) && uniqueNames.Add(name))
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

                    var subDirs = Directory.GetDirectories(basePath);
                    foreach (var dir in subDirs)
                    {
                        string folderName = Path.GetFileName(dir);
                        var lowerFolder = folderName.ToLowerInvariant();
                        if (JunkKeywords.Any(junk => lowerFolder.Contains(junk))) continue;

                        bool hasContent = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories).Length > 0 ||
                                          Directory.GetFiles(dir, "*.url", SearchOption.AllDirectories).Length > 0;

                        if (hasContent && uniqueNames.Add(folderName))
                        {
                            apps.Add(new AppItem
                            {
                                Name = folderName,
                                ExecutablePath = dir,
                                IsUwp = false,
                                FallbackGlyph = "\xE8B7",
                                IconScale = 1.0
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Folder Enumeration Error: {ex.Message}");
                }
            }

            string defaultExplorerPath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\explorer.exe");
            if (uniqueNames.Add("File Explorer") && uniquePaths.Add(defaultExplorerPath))
            {
                apps.Add(new AppItem
                {
                    Name = "File Explorer",
                    ExecutablePath = defaultExplorerPath,
                    IsUwp = false,
                    FallbackGlyph = "\xE838",
                    IconScale = 1.0
                });
            }

            return apps.OrderBy(a => a.Name).ToList();
        }
        #endregion

        #region Shortcut Parsing
        public static string ParseShortcutTarget(string lnkPath) => ParseShortcutTarget(lnkPath, out _);

        public static string ParseShortcutTarget(string lnkPath, out string arguments)
        {
            arguments = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(lnkPath)) return string.Empty;

                if (lnkPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = File.ReadAllLines(lnkPath);
                    var urlLine = lines.FirstOrDefault(l => l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                    if (urlLine != null) return urlLine.Substring(4).Trim();
                }

                try
                {
                    Type? shellAppType = Type.GetTypeFromProgID("Shell.Application");
                    if (shellAppType != null)
                    {
                        dynamic? shell = Activator.CreateInstance(shellAppType);
                        dynamic? folder = shell?.NameSpace(Path.GetDirectoryName(lnkPath));
                        dynamic? folderItem = folder?.ParseName(Path.GetFileName(lnkPath));

                        if (folderItem != null && folderItem!.IsLink)
                        {
                            dynamic link = folderItem!.GetLink;
                            string target = link.Path ?? string.Empty;
                            arguments = link.Arguments ?? string.Empty;

                            if (target.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase) &&
                                arguments.StartsWith(@"shell:appsFolder\", StringComparison.OrdinalIgnoreCase))
                            {
                                return arguments.Substring(17);
                            }

                            if (!string.IsNullOrEmpty(target))
                            {
                                return target;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Shell.Application parsing failed: {ex.Message}");
                }

                IWshRuntimeLibrary.WshShell wsh = new IWshRuntimeLibrary.WshShell();
                IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)wsh.CreateShortcut(lnkPath);
                arguments = shortcut.Arguments?.Trim() ?? string.Empty;
                return shortcut.TargetPath?.Trim().Trim('"', '\'') ?? string.Empty;
            }
            catch { return string.Empty; }
        }
        #endregion

        #region Icon Extraction & Tinting
        public static async Task<ImageSource?> ExtractAppIconAsync(AppItem appItem)
        {
            try
            {
                if (string.IsNullOrEmpty(appItem.ExecutablePath)) return null;

                string? cacheKey = appItem.ExecutablePath ?? appItem.Name;

                if (!string.IsNullOrEmpty(cacheKey) && _iconCache.TryGetValue(cacheKey, out var cachedIcon))
                {
                    appItem.IconSource = cachedIcon;
                    if (appItem.IsIconTinted && appItem.TintedIconSource == null)
                        await ApplyTintAsync(appItem);
                    return cachedIcon;
                }

                ImageSource? resultImage = null;

                if (appItem.IsUwp && appItem.UwpLogoStreamRef != null)
                {
                    IRandomAccessStreamWithContentType stream = await appItem.UwpLogoStreamRef.OpenReadAsync();
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);
                    resultImage = bitmap;
                }
                else
                {
                    string target = appItem.ExecutablePath ?? "";

                    if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        string parsed = ParseShortcutTarget(target);
                        if (!string.IsNullOrEmpty(parsed) && !parsed.Contains("!") && File.Exists(parsed)) target = parsed;
                    }

                    try
                    {
                        StorageFile file = await StorageFile.GetFileFromPathAsync(target);
                        var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 64, ThumbnailOptions.UseCurrentScale);

                        if (thumbnail != null)
                        {
                            var bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(thumbnail);
                            resultImage = bitmapImage;
                        }
                    }
                    catch { }

                    if (resultImage == null && (File.Exists(target) || Directory.Exists(target)))
                    {
                        SHFILEINFO shinfo = new SHFILEINFO();
                        IntPtr res = SHGetFileInfo(target, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);

                        if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                        {
                            try
                            {
                                using var icon = Icon.FromHandle(shinfo.hIcon);
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
                                resultImage = bitmapImage;
                            }
                            finally
                            {
                                DestroyIcon(shinfo.hIcon);
                            }
                        }
                    }
                }

                if (resultImage != null && !string.IsNullOrEmpty(cacheKey))
                {
                    _iconCache[cacheKey] = resultImage;
                    appItem.IconSource = resultImage;

                    if (appItem.IsIconTinted) await ApplyTintAsync(appItem);
                }

                return resultImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Icon extraction failed for {appItem.Name}: {ex.Message}");
            }
            return null;
        }

        public static async Task<IRandomAccessStream?> ExtractAppIconStreamAsync(AppItem appItem)
        {
            try
            {
                if (string.IsNullOrEmpty(appItem.ExecutablePath)) return null;

                if (appItem.IsUwp && appItem.UwpLogoStreamRef != null)
                {
                    return await appItem.UwpLogoStreamRef.OpenReadAsync();
                }

                string target = appItem.ExecutablePath;
                if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string parsed = ParseShortcutTarget(target);
                    if (!string.IsNullOrEmpty(parsed) && File.Exists(parsed)) target = parsed;
                }

                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(target);
                    var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 64, ThumbnailOptions.UseCurrentScale);
                    if (thumbnail != null) return thumbnail;
                }
                catch { }

                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(target, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        using var icon = Icon.FromHandle(shinfo.hIcon);
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
                        return ras;
                    }
                    finally
                    {
                        DestroyIcon(shinfo.hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stream extraction failed for {appItem.Name}: {ex.Message}");
            }
            return null;
        }

        public static async Task ApplyTintAsync(AppItem app)
        {
            if (!app.IsIconTinted || string.IsNullOrEmpty(app.TintColor) || app.TintColor == "NONE")
            {
                app.TintedIconSource = null;
                return;
            }

            try
            {
                string hex = app.TintColor.Split('_')[0].Replace("#", "");
                byte a = 255, r = 0, g = 0, b = 0;

                if (hex.Length == 8)
                {
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                var tintColor = Color.FromArgb(a, r, g, b);

                if (!string.IsNullOrEmpty(app.CustomImagePath) && File.Exists(app.CustomImagePath))
                {
                    var file = await StorageFile.GetFileFromPathAsync(app.CustomImagePath);
                    using var stream = await file.OpenReadAsync();
                    app.TintedIconSource = await IconTintHelper.GetTintedImageAsync(stream, tintColor);
                    return;
                }

                using var iconStream = await ExtractAppIconStreamAsync(app);
                if (iconStream != null)
                {
                    iconStream.Seek(0);
                    app.TintedIconSource = await IconTintHelper.GetTintedImageAsync(iconStream, tintColor);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tint generation failed: {ex.Message}");
            }
        }
        #endregion
    }
}