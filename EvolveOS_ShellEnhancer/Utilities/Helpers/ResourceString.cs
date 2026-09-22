// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Markup;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class ResourceString : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        protected override object ProvideValue()
        {
            return GetValue();
        }

        protected override object ProvideValue(IXamlServiceProvider serviceProvider)
        {
            if (serviceProvider?.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget provideValueTarget)
            {
                if (provideValueTarget.TargetObject is FrameworkElement element)
                {
                    Loc.SetKey(element, Key);
                }
            }

            return GetValue();
        }

        private string GetValue()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return Key;
            }

            return GetString(Key);
        }

        public static string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return LocalizationService.Instance.GetString(key);
        }

        public static string GetString(string key, params object[] args)
        {
            string localizedString = GetString(key);
            if (string.IsNullOrEmpty(localizedString)) return string.Empty;

            try { return string.Format(localizedString, args); }
            catch { return localizedString; }
        }
    }
}