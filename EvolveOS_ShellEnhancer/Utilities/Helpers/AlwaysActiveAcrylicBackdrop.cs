// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    public class AlwaysActiveAcrylicBackdrop : SystemBackdrop
    {
        private DesktopAcrylicController? _acrylicController;
        private SystemBackdropConfiguration? _configuration;

        protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);

            if (_acrylicController != null) return;

            _acrylicController = new DesktopAcrylicController();

            string savedTheme = SettingsEngine.Shell_AppTheme ?? "Default";
            string acrylicStyle = SettingsEngine.Shell_AcrylicStyle ?? "Acrylic"; // "Acrylic" or "AcrylicThin"

            if (acrylicStyle.Equals("AcrylicThin", StringComparison.OrdinalIgnoreCase))
            {
                _acrylicController.Kind = DesktopAcrylicKind.Thin;
            }
            else
            {
                _acrylicController.Kind = DesktopAcrylicKind.Base;
            }

            float opacity = (float)SettingsEngine.Shell_AcrylicOpacity;
            float luminosity = (float)SettingsEngine.Shell_AcrylicLuminosity;

            if (savedTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                _acrylicController.TintColor = Color.FromArgb(255, 32, 32, 32);
                _acrylicController.TintOpacity = opacity > 0 ? opacity : (acrylicStyle.Equals("AcrylicThin") ? 0.35f : 0.65f);
                _acrylicController.FallbackColor = Color.FromArgb(255, 32, 32, 32);
                if (luminosity > 0) _acrylicController.LuminosityOpacity = luminosity;
            }
            else if (savedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                _acrylicController.TintColor = Color.FromArgb(255, 245, 245, 245);
                _acrylicController.TintOpacity = opacity > 0 ? opacity : (acrylicStyle.Equals("AcrylicThin") ? 0.2f : 0.4f);
                _acrylicController.FallbackColor = Color.FromArgb(255, 245, 245, 245);
                if (luminosity > 0) _acrylicController.LuminosityOpacity = luminosity;
            }

            _configuration = new SystemBackdropConfiguration
            {
                IsInputActive = true,
                Theme = GetDesiredTheme(savedTheme)
            };

            _acrylicController.AddSystemBackdropTarget(connectedTarget);
            _acrylicController.SetSystemBackdropConfiguration(_configuration);
        }

        protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
        {
            if (_configuration != null)
            {
                _configuration.Theme = GetDesiredTheme(SettingsEngine.Shell_AppTheme ?? "Default");
            }
        }

        private SystemBackdropTheme GetDesiredTheme(string savedTheme)
        {
            if (savedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                return SystemBackdropTheme.Light;

            if (savedTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                return SystemBackdropTheme.Dark;

            return SystemBackdropTheme.Default;
        }

        protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
        {
            base.OnTargetDisconnected(disconnectedTarget);

            if (_acrylicController != null)
            {
                _acrylicController.RemoveSystemBackdropTarget(disconnectedTarget);
                _acrylicController.Dispose();
                _acrylicController = null;
                _configuration = null;
            }
        }
    }
}