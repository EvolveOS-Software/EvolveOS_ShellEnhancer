// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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
            ["TaskbarPinnedAppsOrder"] = string.Empty,
            ["StartMenuPinnedApps"] = string.Empty,
            ["Taskbar_FilteredFolders"] = string.Empty
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);
        #endregion

        #region Properties
        internal static string TaskbarPinnedAppsOrder { get => (string)_cachedSettings["TaskbarPinnedAppsOrder"]; set => ChangingParameters("TaskbarPinnedAppsOrder", value); }
        internal static string StartMenuPinnedApps { get => (string)_cachedSettings["StartMenuPinnedApps"]; set => ChangingParameters("StartMenuPinnedApps", value); }
        internal static string Taskbar_FilteredFolders { get => (string)_cachedSettings["Taskbar_FilteredFolders"]; set => ChangingParameters("Taskbar_FilteredFolders", value); }
        #endregion

        #region Shell Settings
        internal static bool Shell_MasterEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Shell_MasterEnabled") is object val)
                    {
                        if (val is int intVal) return intVal == 1;
                        if (val is string strVal && bool.TryParse(strVal, out bool boolVal)) return boolVal;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_MasterEnabled Error: {ex.Message}");
                }
                return false;
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\EvolveOS_Optimizer", true);
                    key?.SetValue("Shell_MasterEnabled", value ? 1 : 0, RegistryValueKind.DWord);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Write Shell_MasterEnabled Error: {ex.Message}");
                }
            }
        }

        internal static string Taskbar_Style
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Taskbar_Style") is string val && !string.IsNullOrEmpty(val))
                    {
                        return val;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Taskbar_Style Error: {ex.Message}");
                }
                return "Standard";
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\EvolveOS_Optimizer", true);
                    key?.SetValue("Taskbar_Style", value);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Write Taskbar_Style Error: {ex.Message}");
                }
            }
        }

        internal static int Shell_TaskbarLength
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    var val = key?.GetValue("Shell_TaskbarLength");
                    if (val != null && int.TryParse(val.ToString(), out int size))
                    {
                        return size;
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[Settings] Read Shell_TaskbarLength Error: {ex.Message}"); }
                return 100;
            }
        }

        internal static int Shell_TaskbarCornerRadius
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    var val = key?.GetValue("Shell_TaskbarCornerRadius");
                    if (val != null && int.TryParse(val.ToString(), out int size))
                    {
                        return size;
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[Settings] Read Shell_TaskbarCornerRadius Error: {ex.Message}"); }
                return 8;
            }
        }

        internal static bool Taskbar_PreviewButtons
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Taskbar_PreviewButtons") is string val && bool.TryParse(val, out bool result))
                        return result;
                }
                catch (Exception ex) { Debug.WriteLine($"[Settings] Read Taskbar_PreviewButtons Error: {ex.Message}"); }
                return true;
            }
        }

        internal static bool Taskbar_PreviewAnimation
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Taskbar_PreviewAnimation") is string val && bool.TryParse(val, out bool result))
                        return result;
                }
                catch (Exception ex) { Debug.WriteLine($"[Settings] Read Taskbar_PreviewAnimation Error: {ex.Message}"); }
                return true;
            }
        }

        internal static bool Taskbar_ClockSeconds
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Taskbar_ClockSeconds") is string val && bool.TryParse(val, out bool result))
                        return result;
                }
                catch (Exception ex) { Debug.WriteLine($"[Settings] Read Taskbar_ClockSeconds Error: {ex.Message}"); }
                return false;
            }
        }

        internal static bool Taskbar_ShowUnpinned
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Taskbar_ShowUnpinned") is string val && bool.TryParse(val, out bool result))
                        return result;
                }
                catch (Exception ex) { Debug.WriteLine($"[Settings] Read Taskbar_ShowUnpinned Error: {ex.Message}"); }
                return true;
            }
        }

        internal static bool Taskbar_ShowFoldersAsSubmenus
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Taskbar_ShowFoldersAsSubmenus") is string val && bool.TryParse(val, out bool result))
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Taskbar_ShowFoldersAsSubmenus Error: {ex.Message}");
                }
                return true;
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\EvolveOS_Optimizer");
                    key?.SetValue("Taskbar_ShowFoldersAsSubmenus", value.ToString());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Write Taskbar_ShowFoldersAsSubmenus Error: {ex.Message}");
                }
            }
        }

        internal static string Shell_Language
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Shell_Language") is string val && !string.IsNullOrEmpty(val))
                    {
                        return val;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_Language Error: {ex.Message}");
                }
                return "en-us";
            }
        }

        internal static string Shell_TaskbarAlignment
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Shell_TaskbarAlignment") is string val && !string.IsNullOrEmpty(val))
                    {
                        return val;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read TaskbarAlignment Error: {ex.Message}");
                }
                return "Center";
            }
        }

        internal static string Shell_TaskbarPosition
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Shell_TaskbarPosition") is string val && !string.IsNullOrEmpty(val))
                    {
                        return val;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read TaskbarPosition Error: {ex.Message}");
                }
                return "Bottom";
            }
        }

        internal static bool Shell_StartMenuEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    var val = key?.GetValue("Shell_StartMenuEnabled");

                    if (val != null)
                    {
                        if (val is string strVal && bool.TryParse(strVal, out bool parsedBool))
                        {
                            return parsedBool;
                        }

                        if (val is int intVal)
                        {
                            return intVal == 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_StartMenuEnabled Error: {ex.Message}");
                }
                return false;
            }
        }

        internal static string Shell_StartMenuStyle
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Shell_StartMenuStyle") is string val && !string.IsNullOrEmpty(val))
                    {
                        return val;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_StartMenuStyle Error: {ex.Message}");
                }
                return "SplitStandard";
            }
        }

        internal static string Shell_AppFont
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Shell_AppFont") is string val && !string.IsNullOrEmpty(val))
                    {
                        return val;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_AppFont Error: {ex.Message}");
                }
                return "Segoe UI";
            }
        }

        internal static double Shell_AppFontSize
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    if (key?.GetValue("Shell_AppFontSize") != null)
                    {
                        return Convert.ToDouble(key.GetValue("Shell_AppFontSize"));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_AppFontSize Error: {ex.Message}");
                }
                return 14.0;
            }
        }

        internal static bool Shell_HighPriority
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    var val = key?.GetValue("Shell_HighPriority");
                    if (val != null)
                    {
                        if (bool.TryParse(val.ToString(), out bool result)) return result;
                        if (int.TryParse(val.ToString(), out int intVal)) return intVal != 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_HighPriority Error: {ex.Message}");
                }
                return false;
            }
        }

        internal static int Shell_TaskbarSize
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    var val = key?.GetValue("Shell_TaskbarSize");
                    if (val != null && int.TryParse(val.ToString(), out int size))
                    {
                        return size;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_TaskbarSize Error: {ex.Message}");
                }
                return 48;
            }
        }

        internal static int Shell_TaskbarIconSize
        {
            get
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    var val = key?.GetValue("Shell_TaskbarIconSize");
                    if (val != null && int.TryParse(val.ToString(), out int size))
                    {
                        return size;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_TaskbarIconSize Error: {ex.Message}");
                }
                return 24;
            }
        }

        internal static double Shell_TaskbarPreviewDelay
        {
            get
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
                    var val = key?.GetValue("Shell_TaskbarPreviewDelay");
                    if (val != null && double.TryParse(val.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double delay))
                    {
                        return delay;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Settings] Read Shell_TaskbarPreviewDelay Error: {ex.Message}");
                }
                return 0.5;
            }
        }
        #endregion

        #region Registry Engine
        private static void ChangingParameters(string key, object value)
        {
            _cachedSettings[key] = value;

            try
            {
                using (RegistryKey? regKey = Registry.CurrentUser.CreateSubKey(RegistryPath.SubKey, true))
                {
                    if (regKey != null)
                    {
                        if (value is bool b)
                            regKey.SetValue(key, b ? 1 : 0, RegistryValueKind.DWord);
                        else if (value is int i)
                            regKey.SetValue(key, i, RegistryValueKind.DWord);
                        else
                            regKey.SetValue(key, value.ToString() ?? "", RegistryValueKind.String);

                        regKey.Flush();
                        Debug.WriteLine($"[Settings] SAVED TO: HKCU\\{RegistryPath.SubKey}\\{key} = {value}");
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
                using (RegistryKey? rootKey = Registry.CurrentUser.OpenSubKey(RegistryPath.SubKey, false))
                {
                    foreach (var kv in _defaultSettings)
                    {
                        if (rootKey != null && rootKey.GetValue(kv.Key) != null)
                        {
                            object rawVal = rootKey.GetValue(kv.Key)!;
                            _cachedSettings[kv.Key] = kv.Value switch
                            {
                                bool => Convert.ToInt32(rawVal) != 0,
                                int => Convert.ToInt32(rawVal),
                                _ => rawVal.ToString() ?? kv.Value.ToString()!
                            };
                        }
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
    }
}