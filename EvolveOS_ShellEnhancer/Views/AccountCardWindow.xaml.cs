// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.System;
using WinRT.Interop;

namespace EvolveOS_ShellEnhancer.Views
{
    public sealed partial class AccountCardWindow : Window
    {
        private AppWindow _appWindow;
        private Action<bool> _onDismiss;
        private IntPtr _parentHwnd;
        private bool _isClosing = false;

        public AccountCardWindow(int targetX, int targetY, string name, string email, string accountType, ImageSource profilePic, IntPtr parentHwnd, Action<bool> onDismiss)
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            _parentHwnd = parentHwnd;
            _onDismiss = onDismiss;

            Win32Helper.RemoveWindowBorders(hwnd);

            this.SystemBackdrop = new AlwaysActiveAcrylicBackdrop();

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsAlwaysOnTop = true;
            }

            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(targetX, targetY, 320, 230));

            CardProfileName.Text = name;
            CardAccountType.Text = accountType;

            if (profilePic != null) CardProfilePic.ProfilePicture = profilePic;

            if (string.IsNullOrWhiteSpace(email))
                CardProfileEmail.Visibility = Visibility.Collapsed;
            else
            {
                CardProfileEmail.Text = email;
                CardProfileEmail.Visibility = Visibility.Visible;
            }

            _appWindow.Show();
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            this.Activated += OnWindowActivated;
        }

        private async void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && !_isClosing)
            {
                _isClosing = true;

                await System.Threading.Tasks.Task.Delay(50);

                IntPtr foregroundWindow = GetForegroundWindow();
                bool clickedOutsideBoth = (foregroundWindow != _parentHwnd);

                _onDismiss?.Invoke(clickedOutsideBoth);
                this.Close();
            }
        }

        private async void ActionManageMyAccount_Click(object sender, RoutedEventArgs e)
        {
            try { await Launcher.LaunchUriAsync(new Uri("ms-settings:accounts")); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }

            _onDismiss?.Invoke(true);
            this.Close();
        }

        private async void ActionAccountSettings_Click(object sender, RoutedEventArgs e)
        {
            try { await Launcher.LaunchUriAsync(new Uri("https://account.microsoft.com/")); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }

            _onDismiss?.Invoke(true);
            this.Close();
        }

        private void ActionLock_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", "user32.dll,LockWorkStation") { CreateNoWindow = true });
            _onDismiss?.Invoke(true);
            this.Close();
        }

        private void ActionSignOut_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("shutdown", "/l") { CreateNoWindow = true });
            _onDismiss?.Invoke(true);
            this.Close();
        }
    }
}