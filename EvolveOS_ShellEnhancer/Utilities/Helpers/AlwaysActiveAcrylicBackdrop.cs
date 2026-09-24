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

            if (savedTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                _acrylicController.TintColor = Color.FromArgb(255, 32, 32, 32);
                _acrylicController.TintOpacity = 0.65f;
                _acrylicController.FallbackColor = Color.FromArgb(255, 32, 32, 32);
            }
            else if (savedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                _acrylicController.TintColor = Color.FromArgb(255, 240, 240, 240);
                _acrylicController.TintOpacity = 0.65f;
                _acrylicController.FallbackColor = Color.FromArgb(255, 240, 240, 240);
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