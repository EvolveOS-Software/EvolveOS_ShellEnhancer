// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    public static class Win32Helper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string? windowTitle);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // Base Styles
        private const int GWL_STYLE = -16;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_BORDER = 0x00800000;
        private const int WS_CAPTION = 0x00C00000;
        private const uint WS_POPUP = 0x80000000;

        // Extended Styles
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_CLIENTEDGE = 0x00000200;
        private const int WS_EX_WINDOWEDGE = 0x00000100;
        private const int WS_EX_DLGMODALFRAME = 0x00000001;

        // SetWindowPos Flags
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        // DWM Attributes
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWM_COLOR_NONE = unchecked((int)0xFFFFFFFE);
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        public const int DWMWCP_DEFAULT = 0;
        public const int DWMWCP_DONOTROUND = 1;
        public const int DWMWCP_ROUND = 2;
        public const int DWMWCP_ROUNDSMALL = 3;

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private const byte VK_LWIN = 0x5B;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void RemoveWindowBorders(IntPtr hWnd)
        {
            int style = GetWindowLong(hWnd, GWL_STYLE);
            style &= ~WS_THICKFRAME & ~WS_BORDER & ~WS_CAPTION;
            style |= unchecked((int)WS_POPUP);
            SetWindowLong(hWnd, GWL_STYLE, style);

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            exStyle &= ~WS_EX_WINDOWEDGE & ~WS_EX_CLIENTEDGE & ~WS_EX_DLGMODALFRAME;
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);

            int colorNone = DWM_COLOR_NONE;
            DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, ref colorNone, sizeof(int));

            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static void SetCornerPreference(IntPtr hWnd, int preference)
        {
            DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }

        public static void HideNativeTaskbar()
        {
            IntPtr trayWnd = FindWindow("Shell_TrayWnd", null);
            if (trayWnd != IntPtr.Zero) ShowWindow(trayWnd, SW_HIDE);

            IntPtr secondaryTray = IntPtr.Zero;
            while ((secondaryTray = FindWindowEx(IntPtr.Zero, secondaryTray, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                ShowWindow(secondaryTray, SW_HIDE);
            }
        }

        public static void ShowNativeTaskbar()
        {
            IntPtr trayWnd = FindWindow("Shell_TrayWnd", null);
            if (trayWnd != IntPtr.Zero) ShowWindow(trayWnd, SW_SHOW);

            IntPtr secondaryTray = IntPtr.Zero;
            while ((secondaryTray = FindWindowEx(IntPtr.Zero, secondaryTray, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                ShowWindow(secondaryTray, SW_SHOW);
            }
        }

        public static void OpenNativeStartMenu()
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void OpenQuickSettings()
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event((byte)'A', 0, 0, UIntPtr.Zero);
            keybd_event((byte)'A', 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void OpenTrayOverflow()
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event((byte)'B', 0, 0, UIntPtr.Zero);
            keybd_event((byte)'B', 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            System.Threading.Thread.Sleep(50);

            keybd_event(0x0D, 0, 0, UIntPtr.Zero);
            keybd_event(0x0D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}