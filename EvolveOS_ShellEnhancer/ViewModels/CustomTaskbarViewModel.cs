// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Dispatching;
using Microsoft.Windows.System.Power;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Networking.Connectivity;

namespace EvolveOS_ShellEnhancer.ViewModels
{
    public class CustomTaskbarViewModel
    {
        #region Fields & Properties
        private List<AppItem> _allAppsCache = new();
        private DispatcherQueue? _dispatcherQueue;

        public static readonly HashSet<string> IgnoredSystemProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "SystemSettings", "ApplicationFrameHost", "SearchHost", "StartMenuExperienceHost",
            "ShellExperienceHost", "TextInputHost", "LockApp", "RuntimeBroker", "dwm", "csrss",
            "taskhostw", "EvolveOS_ShellEnhancer", "EvolveOS_Optimizer", "Progman", "WorkerW",
            "cmd", "conhost", "explorer"
        };

        public Action<string>? OnNetworkIconUpdated;
        public Action<Visibility, string, string>? OnBatteryIconUpdated;
        #endregion

        #region System Status Detectors
        public void StartNetworkListener(DispatcherQueue dispatcher)
        {
            _dispatcherQueue = dispatcher;
            UpdateNetworkIcon();
            NetworkInformation.NetworkStatusChanged += (s) => _dispatcherQueue.TryEnqueue(UpdateNetworkIcon);
        }

        private void UpdateNetworkIcon()
        {
            try
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();
                string glyph;

                if (profile == null)
                {
                    glyph = "\xEB55";
                }
                else if (profile.IsWlanConnectionProfile)
                {
                    glyph = "\xE704";
                }
                else if (profile.IsWwanConnectionProfile)
                {
                    glyph = "\xE81C";
                }
                else
                {
                    glyph = "\xE839";
                }

                OnNetworkIconUpdated?.Invoke(glyph);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch network status: {ex.Message}");
                OnNetworkIconUpdated?.Invoke("\xE704");
            }
        }

        public void StartPowerListener(DispatcherQueue dispatcher)
        {
            _dispatcherQueue = dispatcher;
            UpdateBatteryIcon();

            PowerManager.BatteryStatusChanged += (s, e) => _dispatcherQueue.TryEnqueue(UpdateBatteryIcon);
            PowerManager.RemainingChargePercentChanged += (s, e) => _dispatcherQueue.TryEnqueue(UpdateBatteryIcon);
            PowerManager.EnergySaverStatusChanged += (s, e) => _dispatcherQueue.TryEnqueue(UpdateBatteryIcon);
        }

        private void UpdateBatteryIcon()
        {
            try
            {
                var status = toBatteryStatus(PowerManager.BatteryStatus);

                if (status == BatteryStatus.NotPresent)
                {
                    OnBatteryIconUpdated?.Invoke(Visibility.Collapsed, "", "");
                    return;
                }

                int percent = PowerManager.RemainingChargePercent;
                bool isCharging = status == BatteryStatus.Charging || status == BatteryStatus.Idle;

                int iconIndex = (int)Math.Round(percent / 10.0);
                if (iconIndex < 0) iconIndex = 0;
                if (iconIndex > 10) iconIndex = 10;

                int glyphCode;

                if (isCharging)
                {
                    glyphCode = iconIndex == 10 ? 0xE83E : 0xE85A + iconIndex;
                }
                else if (PowerManager.EnergySaverStatus == EnergySaverStatus.On)
                {
                    glyphCode = iconIndex == 10 ? 0xE86E : 0xE864 + iconIndex;
                }
                else
                {
                    glyphCode = iconIndex == 10 ? 0xE83F : 0xE850 + iconIndex;
                }

                string glyph = ((char)glyphCode).ToString();
                string tooltip = $"Battery: {percent}%";

                OnBatteryIconUpdated?.Invoke(Visibility.Visible, glyph, tooltip);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch battery status: {ex.Message}");
                OnBatteryIconUpdated?.Invoke(Visibility.Collapsed, "", "");
            }
        }

        private enum BatteryStatus { NotPresent, Discharging, Idle, Charging }
        private BatteryStatus toBatteryStatus(Microsoft.Windows.System.Power.BatteryStatus status)
        {
            return status switch
            {
                Microsoft.Windows.System.Power.BatteryStatus.Discharging => BatteryStatus.Discharging,
                Microsoft.Windows.System.Power.BatteryStatus.Idle => BatteryStatus.Idle,
                Microsoft.Windows.System.Power.BatteryStatus.Charging => BatteryStatus.Charging,
                _ => BatteryStatus.NotPresent,
            };
        }
        #endregion

        #region Shortcut & Process Parsing
        public List<string> GetPinnedTaskbarApps()
        {
            List<string> pinnedApps = new List<string>();
            try
            {
                string taskbarPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"
                );

                if (Directory.Exists(taskbarPath))
                {
                    string[] shortcuts = Directory.GetFiles(taskbarPath, "*.lnk");
                    foreach (string shortcut in shortcuts)
                    {
                        pinnedApps.Add(shortcut);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load shortcuts: {ex.Message}");
            }

            return pinnedApps;
        }

        public string ParseShortcut(string lnkPath)
        {
            try
            {
                Type? wshShellType = Type.GetTypeFromProgID("WScript.Shell");
                if (wshShellType != null)
                {
                    dynamic shell = Activator.CreateInstance(wshShellType)!;
                    dynamic shortcut = shell.CreateShortcut(lnkPath);

                    string target = shortcut.TargetPath ?? string.Empty;
                    string args = shortcut.Arguments ?? string.Empty;

                    Marshal.ReleaseComObject(shortcut);
                    Marshal.ReleaseComObject(shell);

                    if (target.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase) && args.IndexOf(@"shell:appsfolder\", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int idx = args.IndexOf(@"shell:appsfolder\", StringComparison.OrdinalIgnoreCase);
                        return args.Substring(idx + 17).Trim();
                    }

                    target = target.Trim().Trim('"', '\'');
                    return target;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Shortcut Parse Error: " + ex.Message);
                return string.Empty;
            }
        }

        public void PinItemToTaskbar(string targetPath, Action onComplete)
        {
            Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(targetPath)) return;

                    if (!File.Exists(targetPath) && !Directory.Exists(targetPath)) return;

                    string taskbarPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"
                    );

                    if (!Directory.Exists(taskbarPath))
                    {
                        Directory.CreateDirectory(taskbarPath);
                    }

                    string itemName = System.IO.Path.GetFileNameWithoutExtension(targetPath);
                    if (string.IsNullOrEmpty(itemName))
                    {
                        itemName = System.IO.Path.GetFileName(targetPath);
                    }

                    string lnkPath = System.IO.Path.Combine(taskbarPath, $"{itemName}.lnk");

                    int counter = 1;
                    while (File.Exists(lnkPath))
                    {
                        lnkPath = System.IO.Path.Combine(taskbarPath, $"{itemName} ({counter}).lnk");
                        counter++;
                    }

                    Type? wshShellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (wshShellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(wshShellType)!;
                        dynamic shortcut = shell.CreateShortcut(lnkPath);
                        shortcut.TargetPath = targetPath;
                        shortcut.Save();

                        Marshal.ReleaseComObject(shortcut);
                        Marshal.ReleaseComObject(shell);
                    }

                    onComplete?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to pin item to taskbar: {ex.Message}");
                }
            });
        }

        public string GetProcessNameFromShortcut(string lnkPath)
        {
            string shortcutName = System.IO.Path.GetFileNameWithoutExtension(lnkPath);
            string targetExe = ParseShortcut(lnkPath);
            if (!string.IsNullOrWhiteSpace(targetExe))
            {
                targetExe = Environment.ExpandEnvironmentVariables(targetExe);
            }
            return NormalizeProcessName(shortcutName, shortcutName, targetExe);
        }

        public string NormalizeProcessName(string rawName, string shortcutTitle = "", string targetExePath = "")
        {
            if (!string.IsNullOrWhiteSpace(targetExePath) && File.Exists(targetExePath))
            {
                string exeName = System.IO.Path.GetFileNameWithoutExtension(targetExePath);
                if (!string.IsNullOrEmpty(exeName))
                {
                    return exeName.ToLowerInvariant();
                }
            }

            string name = rawName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
            }

            if (name.Equals("File Explorer", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Windows Explorer", StringComparison.OrdinalIgnoreCase) ||
                shortcutTitle.Contains("Explorer", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                return "explorer";
            }

            return name.ToLowerInvariant();
        }
        #endregion

        #region App Loading & Icon Cache
        public async Task<List<AppItem>> GetAllAppsAsync()
        {
            if (_allAppsCache.Count == 0)
            {
                _allAppsCache = await StartMenuHelper.GetAllAppsAsync();
            }
            return _allAppsCache;
        }

        public async Task LoadIconSafelyAsync(AppItem item)
        {
            try
            {
                var src = await StartMenuHelper.ExtractAppIconAsync(item);
                if (src != null)
                {
                    item.IconSource = src;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Taskbar dynamic icon extraction failed: {ex.Message}");
            }
        }
        #endregion
    }
}