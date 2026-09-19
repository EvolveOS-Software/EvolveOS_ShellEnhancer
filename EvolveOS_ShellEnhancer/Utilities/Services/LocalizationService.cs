// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_ShellEnhancer.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace EvolveOS_ShellEnhancer.Utilities.Services
{
    public enum StringStatus { Found, Fallback, Missing }

    public class LocalizationService : ILocalizationService, INotifyPropertyChanged
    {
        #region Fields & Properties

        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event EventHandler? LanguageChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Dictionary<string, string> _defaultCache = new();
        private readonly Dictionary<string, string> _targetCache = new();
        private readonly Dictionary<string, string> _missingStringsLog = new();
        private readonly object _logLock = new();

        private string _currentLanguage = "en-us";
        private string _currentLanguageCode = "en-us";
        private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

        private static string RealBaseDir
        {
            get
            {
                try
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        return Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
                    }
                }
                catch { }
                return AppContext.BaseDirectory;
            }
        }

        private string LocalizationPath => Path.Combine(RealBaseDir, "ShellEnhancer_Languages");

        public string CurrentLanguage => _currentLanguageCode;
        public bool IsRightToLeft => _currentCulture.TextInfo.IsRightToLeft;

        #endregion

        #region Initialization

        public LocalizationService()
        {
            _instance = this;
            _currentCulture = CultureInfo.CurrentUICulture;
            EnsureLanguageFilesExistLocally();
            LoadDefaultLanguage();
            LoadLanguage("en-us");
        }

        private void EnsureLanguageFilesExistLocally()
        {
            try
            {
                if (!Directory.Exists(LocalizationPath))
                {
                    Directory.CreateDirectory(LocalizationPath);
                }

                var assembly = Assembly.GetExecutingAssembly();

                foreach (string resourceName in assembly.GetManifestResourceNames())
                {
                    if (resourceName.Contains(".Languages.") && resourceName.EndsWith(".xaml"))
                    {
                        string[] parts = resourceName.Split('.');
                        string fileName = $"{parts[parts.Length - 2]}.{parts[parts.Length - 1]}";
                        string filePath = Path.Combine(LocalizationPath, fileName);

                        using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (resourceStream != null)
                            {
                                bool needsExtraction = true;

                                if (File.Exists(filePath))
                                {
                                    try
                                    {
                                        var fileInfo = new FileInfo(filePath);
                                        if (fileInfo.Length == resourceStream.Length)
                                        {
                                            needsExtraction = false;
                                        }
                                    }
                                    catch { }
                                }

                                if (needsExtraction)
                                {
                                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                                    {
                                        resourceStream.CopyTo(fileStream);
                                        fileStream.Flush();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Shell Enhancer Localization] Extraction Error: {ex.Message}");
            }
        }

        #endregion

        #region String Retrieval

        public string GetString(string key) => GetWithStatus(key).Value;
        public string this[string key] => GetString(key);

        public string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            try { return string.Format(format, args); }
            catch { return format; }
        }

        public (string Value, StringStatus Status) GetWithStatus(string key)
        {
            if (string.IsNullOrEmpty(key)) return (string.Empty, StringStatus.Found);

            if (_targetCache.TryGetValue(key, out var targetValue)) return (targetValue, StringStatus.Found);
            if (_defaultCache.TryGetValue(key, out var defaultValue))
            {
                LogMissingString(key, defaultValue);
                return (defaultValue, StringStatus.Fallback);
            }

            LogMissingString(key, "");
            return (key, StringStatus.Missing);
        }

        #endregion

        #region Language Management

        public bool SetLanguage(string languageCode)
        {
            try
            {
                LoadLanguage(languageCode);
                return true;
            }
            catch { return false; }
        }

        public void LoadLanguage(string langCode)
        {
            _currentLanguage = langCode.ToLower();
            _currentLanguageCode = langCode;

            try { _currentCulture = new CultureInfo(langCode); }
            catch { _currentCulture = CultureInfo.InvariantCulture; }

            string filePath = Path.Combine(LocalizationPath, $"{_currentLanguage}.xaml");

            _targetCache.Clear();
            LoadDictionary(filePath, _targetCache);

            LanguageChanged?.Invoke(this, EventArgs.Empty);
            Refresh();
        }

        private void LoadDefaultLanguage()
        {
            string filePath = Path.Combine(LocalizationPath, "en-us.xaml");
            LoadDictionary(filePath, _defaultCache);
        }

        private void LoadDictionary(string filePath, Dictionary<string, string> cache)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var doc = XDocument.Load(fs);
                    XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

                    foreach (var element in doc.Descendants())
                    {
                        var keyAttr = element.Attribute(x + "Key") ?? element.Attribute("Key");
                        if (keyAttr != null) cache[keyAttr.Value] = element.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Shell Enhancer Localization] XML Read Error for {filePath}: {ex.Message}");
            }
        }

        #endregion

        #region Logging & UI

        private void LogMissingString(string key, string fallbackValue)
        {
#if DEBUG
            if (_currentLanguage == "en-us") return;
            lock (_logLock)
            {
                if (!_missingStringsLog.ContainsKey(key))
                {
                    _missingStringsLog[key] = string.IsNullOrEmpty(fallbackValue) ? "NEEDS_TRANSLATION" : fallbackValue;
                    try
                    {
                        string jsonPath = Path.Combine(LocalizationPath, $"MissingStrings_{_currentLanguage}.json");
                        System.IO.File.WriteAllText(jsonPath, System.Text.Json.JsonSerializer.Serialize(_missingStringsLog, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch { }
                }
            }
#endif
        }

        public void Refresh() => OnPropertyChanged("Item[]");

        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}