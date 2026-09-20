// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

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
            _configuration = new SystemBackdropConfiguration
            {
                IsInputActive = true,
                Theme = SystemBackdropTheme.Default
            };

            _acrylicController.AddSystemBackdropTarget(connectedTarget);
            _acrylicController.SetSystemBackdropConfiguration(_configuration);
        }

        protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
        {
            if (_configuration != null)
            {
                _configuration.IsInputActive = true;
            }
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