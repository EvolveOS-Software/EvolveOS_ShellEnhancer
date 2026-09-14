// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    public class AlwaysActiveMicaBackdrop : SystemBackdrop
    {
        private MicaController? _micaController;
        private SystemBackdropConfiguration? _configuration;

        public MicaKind Kind { get; set; } = MicaKind.Base;

        protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);

            _micaController = new MicaController()
            {
                Kind = this.Kind
            };

            _configuration = new SystemBackdropConfiguration
            {
                IsInputActive = true,
                Theme = SystemBackdropTheme.Default
            };

            _micaController.AddSystemBackdropTarget(connectedTarget);
            _micaController.SetSystemBackdropConfiguration(_configuration);
        }

        protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
        {
            base.OnTargetDisconnected(disconnectedTarget);

            if (_micaController != null)
            {
                _micaController.RemoveSystemBackdropTarget(disconnectedTarget);
                _micaController.Dispose();
                _micaController = null;
            }
        }
    }
}