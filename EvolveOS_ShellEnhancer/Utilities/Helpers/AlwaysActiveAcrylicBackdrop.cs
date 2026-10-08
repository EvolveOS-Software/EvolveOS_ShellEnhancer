// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    public class AlwaysActiveAcrylicBackdrop : SystemBackdrop
    {
        private DesktopAcrylicController? _acrylicController;

        public void UpdateLive()
        {
            if (_acrylicController == null) return;

            string savedTheme = SettingsEngine.Shell_AppTheme ?? "Default";
            bool isDark = savedTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            if (savedTheme.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    if (key?.GetValue("SystemUsesLightTheme") is int val) isDark = (val == 0);
                }
                catch { }
            }

            bool isLight = !isDark;

            if (isLight)
            {
                _acrylicController.TintColor = Color.FromArgb(255, 245, 245, 245);
                _acrylicController.TintOpacity = 0.40f;
                _acrylicController.LuminosityOpacity = 0.50f;
            }
            else
            {
                float opacity = (float)SettingsEngine.Shell_AcrylicOpacity;
                if (opacity <= 0) opacity = 0.65f;

                float luminosity = (float)SettingsEngine.Shell_AcrylicLuminosity;
                if (luminosity <= 0) luminosity = 0.50f;

                _acrylicController.TintColor = Color.FromArgb(255, 32, 32, 32);
                _acrylicController.TintOpacity = opacity;

                _acrylicController.LuminosityOpacity = luminosity + 0.001f;
                _acrylicController.LuminosityOpacity = luminosity;
            }

            _acrylicController.FallbackColor = Color.FromArgb(255, 0, 0, 0);
        }

        protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);

            if (_acrylicController != null) return;

            _acrylicController = new DesktopAcrylicController();

            UpdateLive();

            _acrylicController.AddSystemBackdropTarget(connectedTarget);

            string savedTheme = SettingsEngine.Shell_AppTheme ?? "Default";
            bool isDark = savedTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            if (savedTheme.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    if (key?.GetValue("SystemUsesLightTheme") is int val) isDark = (val == 0);
                }
                catch { }
            }

            _acrylicController.SetSystemBackdropConfiguration(new SystemBackdropConfiguration
            {
                IsInputActive = true,
                Theme = isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light
            });
        }

        protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
        {
            base.OnTargetDisconnected(disconnectedTarget);
            if (_acrylicController != null)
            {
                _acrylicController.RemoveSystemBackdropTarget(disconnectedTarget);
                _acrylicController.Dispose();
                _acrylicController = null;
            }
        }
    }
}