// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using Windows.System;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    public static class Win32Helper
    {
        #region Native Methods & P/Invokes

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ShowCursor(bool bShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("dwmapi.dll", EntryPoint = "#113")]
        public static extern int DwmpActivateLivePreview(uint enable, IntPtr hWnd, IntPtr top, uint peekType);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SendNotifyMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        #endregion

        #region DWM Thumbnail API

        [DllImport("dwmapi.dll")]
        public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [DllImport("dwmapi.dll")]
        public static extern int DwmUnregisterThumbnail(IntPtr thumb);

        [DllImport("dwmapi.dll")]
        public static extern int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fVisible;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fSourceClientAreaOnly;
        }

        public const int DWM_TNP_RECTDESTINATION = 0x00000001;
        public const int DWM_TNP_VISIBLE = 0x00000008;
        public const int DWM_TNP_OPACITY = 0x00000004;

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        #endregion

        #region Constants

        public const int SW_RESTORE = 9;

        // Base Styles
        public const int GWL_STYLE = -16;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_BORDER = 0x00800000;
        public const int WS_CAPTION = 0x00C00000;
        public const uint WS_POPUP = 0x80000000;
        public const long WS_POPUP_LONG = 0x80000000L;
        public const long WS_CAPTION_LONG = 0x00C00000L;
        public const long WS_THICKFRAME_LONG = 0x00040000L;
        public const long WS_BORDER_LONG = 0x00800000L;

        // Extended Styles
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_CLIENTEDGE = 0x00000200;
        public const int WS_EX_WINDOWEDGE = 0x00000100;
        public const int WS_EX_DLGMODALFRAME = 0x00000001;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const long WS_EX_TOOLWINDOW_LONG = 0x00000080L;
        public const long WS_EX_TOPMOST_LONG = 0x00000008L;
        public const long WS_EX_TOPMOST = 0x00000008L;

        // Window Parent / Index Constants
        public const int GWLP_HWNDPARENT = -8;

        // SetWindowPos Flags
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint WM_SETTINGCHANGE = 0x001A;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_TOP = new IntPtr(0);

        // DWM Attributes
        public const int DWMWA_BORDER_COLOR = 34;
        public const int DWM_COLOR_NONE = unchecked((int)0xFFFFFFFE);
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        public const int DWMWCP_DEFAULT = 0;
        public const int DWMWCP_DONOTROUND = 1;
        public const int DWMWCP_ROUND = 2;
        public const int DWMWCP_ROUNDSMALL = 3;

        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;

        public const byte VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;

        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;

        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;

        public const uint WM_CHANGEUISTATE = 0x0127;
        public const uint LWA_ALPHA = 0x2;

        public const int DWMWA_EXCLUDED_FROM_PEEK = 12;

        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        public const uint ABM_NEW = 0x0000;
        public const uint ABM_REMOVE = 0x0001;
        public const uint ABM_QUERYPOS = 0x0002;
        public const uint ABM_SETPOS = 0x0003;
        public const uint ABM_GETTASKBARPOS = 0x00000005;

        public const uint ABE_LEFT = 0;
        public const uint ABE_TOP = 1;
        public const uint ABE_RIGHT = 2;
        public const uint ABE_BOTTOM = 3;

        public const uint GW_OWNER = 4;
        public const uint WM_CLOSE = 0x0010;
        public const int DWMWA_CLOAKED = 14;

        #endregion

        #region Fields

        public static bool IsSimulating = false;

        #endregion

        #region Window Management

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

        private static void ForceGhostWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            ShowWindow(hWnd, SW_HIDE);

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            SetLayeredWindowAttributes(hWnd, 0, 0, LWA_ALPHA);
        }

        private static void ForceRestoreWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
            SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);

            ShowWindow(hWnd, SW_SHOW);
        }

        public static void HideNativeTaskbar()
        {
            ForceGhostWindow(FindWindow("Shell_TrayWnd", null));

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder sb = new StringBuilder(256);
                GetClassName(hWnd, sb, sb.Capacity);

                if (sb.ToString() == "Shell_SecondaryTrayWnd")
                {
                    ForceGhostWindow(hWnd);
                }
                return true;
            }, IntPtr.Zero);
        }

        public static void ShowNativeTaskbar()
        {
            ForceRestoreWindow(FindWindow("Shell_TrayWnd", null));

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder sb = new StringBuilder(256);
                GetClassName(hWnd, sb, sb.Capacity);

                if (sb.ToString() == "Shell_SecondaryTrayWnd")
                {
                    ForceRestoreWindow(hWnd);
                }
                return true;
            }, IntPtr.Zero);
        }

        public static void PreventFocusStealing(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            exStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
        }

        public static void SetNativeTaskbarPosition(string position)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true);
                if (key != null)
                {
                    int dwordValue = 3;
                    switch (position)
                    {
                        case "Left": dwordValue = 0; break;
                        case "Top": dwordValue = 1; break;
                        case "Right": dwordValue = 2; break;
                        case "Bottom": dwordValue = 3; break;
                    }

                    key.SetValue("TaskbarLocation", dwordValue, Microsoft.Win32.RegistryValueKind.DWord);

                    SendNotifyMessage(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "TraySettings");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to move native taskbar: {ex.Message}");
            }
        }

        #endregion

        #region Shell Interaction Methods

        public static void OpenNativeStartMenu()
        {
            IsSimulating = true;
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            IsSimulating = false;
        }

        public static async Task ToggleQuickSettingsAsync()
        {
            IntPtr ccHwnd = FindWindow("ControlCenterWindow", null);

            if (ccHwnd != IntPtr.Zero && IsWindowVisible(ccHwnd))
            {
                PostMessage(ccHwnd, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-actioncenter:controlcenter/true"));

                await Task.Delay(180);
                IntPtr newCcHwnd = FindWindow("ControlCenterWindow", null);
                if (newCcHwnd != IntPtr.Zero)
                {
                    SendMessage(newCcHwnd, 0x0127, (IntPtr)0x00010001, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static async Task ToggleCalendarAsync()
        {
            IntPtr fg = GetForegroundWindow();
            StringBuilder sb = new StringBuilder(256);
            GetClassName(fg, sb, sb.Capacity);
            string className = sb.ToString();

            if (className == "Windows.UI.Core.CoreWindow" || className.Contains("ActionCenter"))
            {
                IsSimulating = true;
                keybd_event(0x1B, 0, 0, UIntPtr.Zero);
                keybd_event(0x1B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                IsSimulating = false;
                return;
            }

            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-actioncenter:"));
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static async Task OpenTrayOverflowAsync()
        {
            IntPtr fg = GetForegroundWindow();
            StringBuilder sb = new StringBuilder(256);
            GetClassName(fg, sb, sb.Capacity);
            string className = sb.ToString();

            if (className.Contains("Overflow") || className.Contains("NotifyIcon"))
            {
                IsSimulating = true;
                keybd_event(0x1B, 0, 0, UIntPtr.Zero);
                keybd_event(0x1B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                IsSimulating = false;
                return;
            }

            IsSimulating = true;
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event((byte)'B', 0, 0, UIntPtr.Zero);
            keybd_event((byte)'B', 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            IsSimulating = false;

            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd != IntPtr.Zero)
            {
                SendMessage(taskbarHwnd, 0x0127, (IntPtr)0x00010001, IntPtr.Zero);
            }

            await Task.Delay(50);

            IsSimulating = true;
            keybd_event(0x0D, 0, 0, UIntPtr.Zero);
            await Task.Delay(20);
            keybd_event(0x0D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            IsSimulating = false;

            await Task.Delay(10);

            IntPtr newFg = GetForegroundWindow();

            if (GetWindowRect(newFg, out RECT rect))
            {
                if (GetCursorPos(out POINT originalPos))
                {
                    ShowCursor(false);
                    try
                    {
                        SetCursorPos(rect.Left + 15, rect.Top + 15);

                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

                        SetCursorPos(originalPos.X, originalPos.Y);
                    }
                    finally
                    {
                        ShowCursor(true);
                    }
                }
            }
        }

        public static void SetNativeStartMenuAlignment(bool alignLeft)
        {
            try
            {
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                {
                    if (key != null)
                    {
                        int alignmentValue = alignLeft ? 0 : 1;
                        key.SetValue("TaskbarAl", alignmentValue, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Registry Error: " + ex.Message);
            }
        }

        #endregion

        #region Universal Helpers

        public static RECT GetTaskbarRect()
        {
            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd != IntPtr.Zero && GetWindowRect(taskbarHwnd, out RECT rect))
            {
                return rect;
            }
            return new RECT { Left = 0, Top = 1040, Right = 1920, Bottom = 1080 };
        }

        public static uint GetTaskbarEdge()
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
            return abd.uEdge;
        }

        public static int GetCurrentWidgetOffset(IntPtr monitorHwnd)
        {
            GetWindowRect(monitorHwnd, out RECT windowRect);
            var taskbarRect = GetTaskbarRect();
            uint edge = GetTaskbarEdge();

            if (edge == 0 || edge == 2)
            {
                return taskbarRect.Bottom - windowRect.Bottom;
            }
            else
            {
                return taskbarRect.Right - windowRect.Right;
            }
        }

        public static bool AreRectsEqual(RECT a, RECT b)
        {
            return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        }

        #endregion
    }
}