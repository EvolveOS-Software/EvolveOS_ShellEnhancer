// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;
using System.Globalization;

namespace EvolveOS_ShellEnhancer.Utilities.Managers
{
    internal sealed class СheckingGlobalParameters
    {
        #region Initialization
        internal static void Initialize()
        {
            try
            {
                SettingsEngine.CheckingParameters();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
        #endregion
    }

    internal sealed class SettingsEngine
    {
        #region Fields & Defaults
        private static readonly Dictionary<string, object> _defaultSettings = new Dictionary<string, object>
        {
            #region Properties
            ["TaskbarPinnedAppsOrder"] = string.Empty,
            ["StartMenuPinnedApps"] = string.Empty,
            ["Taskbar_FilteredFolders"] = string.Empty,
            #endregion

            #region Shell Settings
            ["Shell_MasterEnabled"] = false,
            ["Shell_RunOnStartup"] = false,
            ["Shell_AppTheme"] = "Default",
            ["Shell_TaskbarEnabled"] = false,
            ["Shell_TaskbarStyle"] = "Standard",
            ["Shell_TaskbarLength"] = 100,
            ["Shell_TaskbarCornerRadius"] = 8,
            ["Shell_TaskbarPreviewButtons"] = true,
            ["Shell_TaskbarPreviewAnimation"] = true,
            ["Shell_TaskbarClockSeconds"] = false,
            ["Shell_TaskbarShowUnpinned"] = true,
            ["Taskbar_ShowFoldersAsSubmenus"] = true,
            ["Shell_Language"] = "en-us",
            ["Shell_TaskbarAlignment"] = "Center",
            ["Shell_TaskbarPosition"] = "Bottom",
            ["Shell_StartMenuEnabled"] = false,
            ["Shell_StartMenuStyle"] = "Standard",
            ["Shell_StartMenuAnimation"] = true,
            ["Shell_StartMenuAnimStyle"] = "Standard",
            ["Shell_StartMenuAnimSpeed"] = 1.0,
            ["Shell_AppFont"] = "Segoe UI",
            ["Shell_AppFontSize"] = 14.0,
            ["Shell_HighPriority"] = false,
            ["Shell_TaskbarSize"] = 48,
            ["Shell_TaskbarIconSize"] = 24,
            ["Shell_TaskbarPreviewDelay"] = 0.5,
            ["Shell_TaskbarPreviewAnimStyle"] = "Standard",
            ["Shell_TaskbarPreviewAnimSpeed"] = 1.0,
            ["Shell_StartMenuProfileClick"] = true,

            ["Shell_StartMenuPowerSleep"] = true,
            ["Shell_StartMenuPowerRestartBios"] = false,
            ["Shell_StartMenuPowerLogOff"] = false,
            ["Shell_StartMenuRecentDocs"] = false,
            ["Shell_StartMenuShortcuts"] = "Documents|Standard::Documents|0|0|;Downloads|Standard::Downloads|0|0|;Music|Standard::Music|0|0|;Pictures|Standard::Pictures|0|0|;Settings|ms-settings:|0|0|;Run|Standard::Run|0|0|;Control Panel|control.exe|0|0|",
            ["Shell_TaskbarAnimation"] = "Spring",
            ["Shell_TaskbarHoverAnimation"] = "Standard",
            ["Shell_TaskbarHoverBackground"] = true,
            ["Shell_TaskbarMonitorAware"] = false,
            ["Shell_TaskbarUnpinnedMode"] = "Inline"
            #endregion
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);
        #endregion

        #region Properties
        internal static string TaskbarPinnedAppsOrder { get => (string)_cachedSettings["TaskbarPinnedAppsOrder"]; set => ChangingParameters("TaskbarPinnedAppsOrder", value); }
        internal static string StartMenuPinnedApps { get => (string)_cachedSettings["StartMenuPinnedApps"]; set => ChangingParameters("StartMenuPinnedApps", value); }
        internal static string Taskbar_FilteredFolders { get => (string)_cachedSettings["Taskbar_FilteredFolders"]; set => ChangingParameters("Taskbar_FilteredFolders", value); }
        #endregion

        #region Shell Settings
        internal static bool Shell_MasterEnabled { get => (bool)_cachedSettings["Shell_MasterEnabled"]; set => ChangingParameters("Shell_MasterEnabled", value); }
        internal static bool Shell_RunOnStartup { get => (bool)_cachedSettings["Shell_RunOnStartup"]; set => ChangingParameters("Shell_RunOnStartup", value); }
        internal static string Shell_AppTheme { get => _cachedSettings["Shell_AppTheme"]?.ToString() ?? "Default"; set => ChangingParameters("Shell_AppTheme", value); }
        internal static bool Shell_TaskbarEnabled { get => (bool)_cachedSettings["Shell_TaskbarEnabled"]; set => ChangingParameters("Shell_TaskbarEnabled", value); }
        internal static string Shell_TaskbarStyle { get => (string)_cachedSettings["Shell_TaskbarStyle"]; set => ChangingParameters("Shell_TaskbarStyle", value); }
        internal static int Shell_TaskbarLength { get => (int)_cachedSettings["Shell_TaskbarLength"]; set => ChangingParameters("Shell_TaskbarLength", value); }
        internal static int Shell_TaskbarCornerRadius { get => (int)_cachedSettings["Shell_TaskbarCornerRadius"]; set => ChangingParameters("Shell_TaskbarCornerRadius", value); }
        internal static bool Shell_TaskbarPreviewButtons { get => (bool)_cachedSettings["Shell_TaskbarPreviewButtons"]; set => ChangingParameters("Shell_TaskbarPreviewButtons", value); }
        internal static bool Shell_TaskbarPreviewAnimation { get => (bool)_cachedSettings["Shell_TaskbarPreviewAnimation"]; set => ChangingParameters("Shell_TaskbarPreviewAnimation", value); }
        internal static bool Shell_TaskbarClockSeconds { get => (bool)_cachedSettings["Shell_TaskbarClockSeconds"]; set => ChangingParameters("Shell_TaskbarClockSeconds", value); }
        internal static bool Shell_TaskbarShowUnpinned { get => (bool)_cachedSettings["Shell_TaskbarShowUnpinned"]; set => ChangingParameters("Shell_TaskbarShowUnpinned", value); }
        internal static bool Taskbar_ShowFoldersAsSubmenus { get => (bool)_cachedSettings["Taskbar_ShowFoldersAsSubmenus"]; set => ChangingParameters("Taskbar_ShowFoldersAsSubmenus", value); }
        internal static string Shell_Language { get => (string)_cachedSettings["Shell_Language"]; set => ChangingParameters("Shell_Language", value); }
        internal static string Shell_TaskbarAlignment { get => (string)_cachedSettings["Shell_TaskbarAlignment"]; set => ChangingParameters("Shell_TaskbarAlignment", value); }
        internal static string Shell_TaskbarPosition { get => (string)_cachedSettings["Shell_TaskbarPosition"]; set => ChangingParameters("Shell_TaskbarPosition", value); }
        internal static bool Shell_StartMenuEnabled { get => (bool)_cachedSettings["Shell_StartMenuEnabled"]; set => ChangingParameters("Shell_StartMenuEnabled", value); }
        internal static string Shell_StartMenuStyle { get => (string)_cachedSettings["Shell_StartMenuStyle"]; set => ChangingParameters("Shell_StartMenuStyle", value); }
        internal static bool Shell_StartMenuAnimation { get => (bool)_cachedSettings["Shell_StartMenuAnimation"]; set => ChangingParameters("Shell_StartMenuAnimation", value); }
        internal static string Shell_StartMenuAnimStyle { get => (string)_cachedSettings["Shell_StartMenuAnimStyle"]; set => ChangingParameters("Shell_StartMenuAnimStyle", value); }
        internal static double Shell_StartMenuAnimSpeed { get => Convert.ToDouble(_cachedSettings["Shell_StartMenuAnimSpeed"]); set => ChangingParameters("Shell_StartMenuAnimSpeed", value); }
        internal static string Shell_AppFont { get => (string)_cachedSettings["Shell_AppFont"]; set => ChangingParameters("Shell_AppFont", value); }
        internal static double Shell_AppFontSize { get => Convert.ToDouble(_cachedSettings["Shell_AppFontSize"]); set => ChangingParameters("Shell_AppFontSize", value); }
        internal static bool Shell_HighPriority { get => (bool)_cachedSettings["Shell_HighPriority"]; set => ChangingParameters("Shell_HighPriority", value); }
        internal static int Shell_TaskbarSize { get => (int)_cachedSettings["Shell_TaskbarSize"]; set => ChangingParameters("Shell_TaskbarSize", value); }
        internal static int Shell_TaskbarIconSize { get => (int)_cachedSettings["Shell_TaskbarIconSize"]; set => ChangingParameters("Shell_TaskbarIconSize", value); }
        internal static double Shell_TaskbarPreviewDelay { get => Convert.ToDouble(_cachedSettings["Shell_TaskbarPreviewDelay"]); set => ChangingParameters("Shell_TaskbarPreviewDelay", value); }
        internal static string Shell_TaskbarPreviewAnimStyle { get => (string)_cachedSettings["Shell_TaskbarPreviewAnimStyle"]; set => ChangingParameters("Shell_TaskbarPreviewAnimStyle", value); }
        internal static double Shell_TaskbarPreviewAnimSpeed { get => Convert.ToDouble(_cachedSettings["Shell_TaskbarPreviewAnimSpeed"]); set => ChangingParameters("Shell_TaskbarPreviewAnimSpeed", value); }
        internal static bool Shell_StartMenuProfileClick { get => (bool)_cachedSettings["Shell_StartMenuProfileClick"]; set => ChangingParameters("Shell_StartMenuProfileClick", value); }
        internal static bool Shell_StartMenuPowerSleep { get => (bool)_cachedSettings["Shell_StartMenuPowerSleep"]; set => ChangingParameters("Shell_StartMenuPowerSleep", value); }
        internal static bool Shell_StartMenuPowerRestartBios { get => (bool)_cachedSettings["Shell_StartMenuPowerRestartBios"]; set => ChangingParameters("Shell_StartMenuPowerRestartBios", value); }
        internal static bool Shell_StartMenuPowerLogOff { get => (bool)_cachedSettings["Shell_StartMenuPowerLogOff"]; set => ChangingParameters("Shell_StartMenuPowerLogOff", value); }
        internal static bool Shell_StartMenuRecentDocs { get => (bool)_cachedSettings["Shell_StartMenuRecentDocs"]; set => ChangingParameters("Shell_StartMenuRecentDocs", value); }
        internal static string Shell_StartMenuShortcuts { get => (string)_cachedSettings["Shell_StartMenuShortcuts"]; set => ChangingParameters("Shell_StartMenuShortcuts", value); }
        internal static string Shell_TaskbarAnimation { get => (string)_cachedSettings["Shell_TaskbarAnimation"]; set => ChangingParameters("Shell_TaskbarAnimation", value); }
        internal static string Shell_TaskbarHoverAnimation { get => (string)_cachedSettings["Shell_TaskbarHoverAnimation"]; set => ChangingParameters("Shell_TaskbarHoverAnimation", value); }
        internal static bool Shell_TaskbarHoverBackground { get => (bool)_cachedSettings["Shell_TaskbarHoverBackground"]; set => ChangingParameters("Shell_TaskbarHoverBackground", value); }
        internal static bool Shell_TaskbarMonitorAware { get => (bool)_cachedSettings["Shell_TaskbarMonitorAware"]; set => ChangingParameters("Shell_TaskbarMonitorAware", value); }
        internal static string Shell_TaskbarUnpinnedMode { get => (string)_cachedSettings["Shell_TaskbarUnpinnedMode"]; set => ChangingParameters("Shell_TaskbarUnpinnedMode", value); }
        #endregion

        #region Registry Engine
        private static string GetRegistryPath(string key)
        {
            if (key == "TaskbarPinnedAppsOrder" || key == "StartMenuPinnedApps" || key == "Taskbar_FilteredFolders")
            {
                return RegistryPath.SubKey;
            }

            return @"Software\EvolveOS_Optimizer";
        }

        private static void ChangingParameters(string key, object value)
        {
            _cachedSettings[key] = value;

            try
            {
                string targetPath = GetRegistryPath(key);
                using (RegistryKey? regKey = Registry.CurrentUser.CreateSubKey(targetPath, true))
                {
                    if (regKey != null)
                    {
                        if (value is bool b)
                            regKey.SetValue(key, b ? 1 : 0, RegistryValueKind.DWord);
                        else if (value is int i)
                            regKey.SetValue(key, i, RegistryValueKind.DWord);
                        else if (value is double d)
                            regKey.SetValue(key, d.ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
                        else
                            regKey.SetValue(key, value.ToString() ?? "", RegistryValueKind.String);

                        regKey.Flush();
                        Debug.WriteLine($"[Settings] SAVED TO: HKCU\\{targetPath}\\{key} = {value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] WRITE ERROR: {ex.Message}");
            }
        }

        internal static void CheckingParameters()
        {
            try
            {
                using RegistryKey? optimizerKey = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                using RegistryKey? enhancerKey = Registry.CurrentUser.OpenSubKey(RegistryPath.SubKey, false);

                foreach (var kv in _defaultSettings)
                {
                    try
                    {
                        RegistryKey? targetKey = (kv.Key == "TaskbarPinnedAppsOrder" || kv.Key == "StartMenuPinnedApps" || kv.Key == "Taskbar_FilteredFolders")
                            ? enhancerKey
                            : optimizerKey;

                        if (targetKey != null)
                        {
                            object? rawVal = targetKey.GetValue(kv.Key);
                            if (rawVal != null)
                            {
                                string strVal = rawVal.ToString() ?? "";

                                if (kv.Value is bool)
                                {
                                    if (int.TryParse(strVal, out int intBool))
                                        _cachedSettings[kv.Key] = intBool != 0;
                                    else if (bool.TryParse(strVal, out bool bVal))
                                        _cachedSettings[kv.Key] = bVal;
                                    else
                                        _cachedSettings[kv.Key] = (strVal == "1");
                                }
                                else if (kv.Value is int)
                                {
                                    if (int.TryParse(strVal, out int iVal))
                                        _cachedSettings[kv.Key] = iVal;
                                }
                                else if (kv.Value is double)
                                {
                                    string safeDouble = strVal.Replace(',', '.');
                                    if (double.TryParse(safeDouble, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                                        _cachedSettings[kv.Key] = dVal;
                                }
                                else
                                {
                                    _cachedSettings[kv.Key] = strVal;
                                }
                            }
                        }
                    }
                    catch (Exception itemEx)
                    {
                        Debug.WriteLine($"[Settings] Failed to load {kv.Key}: {itemEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] CheckingParameters Error: {ex.Message}");
            }
        }
        #endregion

        #region Registry
        internal static class RegistryPath
        {
            internal const string SubKey = @"Software\EvolveOS_Optimizer\EvolveOS_ShellEnhancer";
            internal static readonly string BaseKey = @$"HKEY_CURRENT_USER\{SubKey}";
        }
        #endregion

        #region Self Reboot

        internal static async void SelfReboot(string injectedCommand = "")
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (!string.IsNullOrEmpty(exePath))
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string extra = string.IsNullOrWhiteSpace(injectedCommand) ? "" : $"{injectedCommand}; ";

                string psScript = $"{extra}Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 500; Start-Process -FilePath \"{exePath}\"";

                await CommandExecutor.RunCommand(psScript, isPowerShell: true, waitForExit: false);
            }

            App.ExitApp();
        }

        #endregion
    }
}