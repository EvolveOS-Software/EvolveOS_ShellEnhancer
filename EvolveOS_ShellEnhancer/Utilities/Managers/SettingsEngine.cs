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
    }

    internal sealed class SettingsEngine
    {
        private static readonly Dictionary<string, object> _defaultSettings = new Dictionary<string, object>
        {
            ["TaskbarPinnedAppsOrder"] = string.Empty,
            ["StartMenuPinnedApps"] = string.Empty
        };

        private static readonly Dictionary<string, object> _cachedSettings = new Dictionary<string, object>(_defaultSettings);

        internal static string TaskbarPinnedAppsOrder { get => (string)_cachedSettings["TaskbarPinnedAppsOrder"]; set => ChangingParameters("TaskbarPinnedAppsOrder", value); }
        internal static string StartMenuPinnedApps { get => (string)_cachedSettings["StartMenuPinnedApps"]; set => ChangingParameters("StartMenuPinnedApps", value); }

        #region Shell Settings
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
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\EvolveOS_Optimizer", false);
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
        #endregion

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

        #region Registry
        internal static class RegistryPath
        {
            internal const string SubKey = @"Software\EvolveOS_Optimizer\EvolveOS_ShellEnhancer";
            internal static readonly string BaseKey = @$"HKEY_CURRENT_USER\{SubKey}";
        }
        #endregion
    }
}