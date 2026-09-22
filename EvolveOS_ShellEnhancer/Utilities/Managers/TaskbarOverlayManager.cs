// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace EvolveOS_ShellEnhancer.Utilities.Managers
{
    public static class TaskbarOverlayManager
    {
        #region Method 1: The "Taskbar Parenting" Approach (Recommended)
        public static void InjectIntoTaskbar(IntPtr monitorHwnd)
        {
            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd == IntPtr.Zero) return;

            SetWindowLongPtr(monitorHwnd, GWLP_HWNDPARENT, taskbarHwnd);

            long style = GetWindowLongPtr(monitorHwnd, GWL_STYLE).ToInt64();
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER);
            style |= WS_POPUP;
            SetWindowLongPtr(monitorHwnd, GWL_STYLE, new IntPtr(style));

            long exStyle = GetWindowLongPtr(monitorHwnd, GWL_EXSTYLE).ToInt64();
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            SetWindowLongPtr(monitorHwnd, GWL_EXSTYLE, new IntPtr(exStyle));

            SetWindowPos(monitorHwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static void PositionInsideTaskbar(IntPtr monitorHwnd, int primaryOffset, int widgetWidth, int widgetHeight)
        {
            var taskbarRect = GetTaskbarRect();
            uint edge = GetTaskbarEdge();

            int absoluteX = 0;
            int absoluteY = 0;

            switch (edge)
            {
                case 1:
                case 3:
                    absoluteX = taskbarRect.Right - primaryOffset - widgetWidth;
                    absoluteY = taskbarRect.Top + ((taskbarRect.Height - widgetHeight) / 2);
                    break;

                case 0:
                case 2:
                    absoluteX = taskbarRect.Left + ((taskbarRect.Width - widgetWidth) / 2);
                    absoluteY = taskbarRect.Bottom - primaryOffset - widgetHeight;
                    break;
            }

            SetWindowPos(monitorHwnd, HWND_TOPMOST, absoluteX, absoluteY, 0, 0,
                SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }
        #endregion

        #region Method 2: The "Floating Phantom" Approach (Legacy)
        private const long WS_EX_NOREDIRECTIONBITMAP = 0x00200000L;

        public static void ApplyWidgetStyles(IntPtr monitorHwnd)
        {
            long exStyle = GetWindowLongPtr(monitorHwnd, GWL_EXSTYLE).ToInt64();
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP;
            SetWindowLongPtr(monitorHwnd, GWL_EXSTYLE, new IntPtr(exStyle));

            SetWindowPos(monitorHwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static void SnapToCoordinates(IntPtr monitorHwnd, int x, int y)
        {
            SetWindowPos(monitorHwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }

        public static void EnsureTopmost(IntPtr monitorHwnd)
        {
            SetWindowPos(monitorHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        #endregion

        #region Visibility & Fullscreen Detection
        public static bool ShouldHideWidget()
        {
            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd == IntPtr.Zero) return false;

            IntPtr tbMonitor = MonitorFromWindow(taskbarHwnd, MONITOR_DEFAULTTONEAREST);

            GetWindowRect(taskbarHwnd, out RECT tbRect);
            MONITORINFO miTb = new MONITORINFO();
            miTb.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

            if (GetMonitorInfo(tbMonitor, ref miTb))
            {
                uint edge = GetTaskbarEdge();

                if (edge == 3 && tbRect.Top >= miTb.rcMonitor.Bottom - 10) return true;
                if (edge == 1 && tbRect.Bottom <= miTb.rcMonitor.Top + 10) return true;
                if (edge == 0 && tbRect.Right <= miTb.rcMonitor.Left + 10) return true;
                if (edge == 2 && tbRect.Left >= miTb.rcMonitor.Right - 10) return true;
            }

            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                IntPtr desktopHwnd = FindWindow("Progman", null);
                IntPtr workerwHwnd = FindWindow("WorkerW", null);

                if (fgHwnd != desktopHwnd && fgHwnd != workerwHwnd)
                {
                    IntPtr fgMonitor = MonitorFromWindow(fgHwnd, MONITOR_DEFAULTTONEAREST);

                    if (fgMonitor == tbMonitor)
                    {
                        GetWindowRect(fgHwnd, out RECT fgRect);

                        if (fgRect.Left <= miTb.rcMonitor.Left &&
                            fgRect.Top <= miTb.rcMonitor.Top &&
                            fgRect.Right >= miTb.rcMonitor.Right &&
                            fgRect.Bottom >= miTb.rcMonitor.Bottom)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        #endregion
    }
}