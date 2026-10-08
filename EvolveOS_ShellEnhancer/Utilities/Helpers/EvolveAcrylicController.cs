// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    public class EvolveAcrylicController
    {
        #region Win32 & DWM API Imports
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        private static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND pBlurBehind);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern IntPtr GetStockObject(int fnObject);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
        private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLongW")]
        private static extern IntPtr SetClassLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_BLURBEHIND
        {
            public uint dwFlags;
            public int fEnable;
            public IntPtr hRgnBlur;
            public int fTransitionOnMaximized;
        }
        #endregion

        private readonly IntPtr _hWnd;
        private bool _isInitialized = false;

        public EvolveAcrylicController(IntPtr hWnd)
        {
            _hWnd = hWnd;
        }

        public void Initialize(bool isLightMode)
        {
            if (_hWnd == IntPtr.Zero || _isInitialized) return;

            MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(_hWnd, ref margins);

            IntPtr hRgn = CreateRectRgn(0, 0, -1, -1);
            var blurBehind = new DWM_BLURBEHIND
            {
                dwFlags = 1 | 2 | 4,
                fEnable = 1,
                hRgnBlur = hRgn,
                fTransitionOnMaximized = 1
            };
            DwmEnableBlurBehindWindow(_hWnd, ref blurBehind);
            DeleteObject(hRgn);

            int darkVal = isLightMode ? 0 : 1;
            DwmSetWindowAttribute(_hWnd, 20, ref darkVal, 4);
            DwmSetWindowAttribute(_hWnd, 19, ref darkVal, 4);

            IntPtr bgBrush = GetStockObject(isLightMode ? 0 : 4);
            SetClassBrush(_hWnd, bgBrush);
            EnumChildWindows(_hWnd, (childHwnd, lParam) =>
            {
                SetClassBrush(childHwnd, bgBrush);
                return true;
            }, IntPtr.Zero);

            _isInitialized = true;
        }

        public void UpdateStyle(string acrylicStyle, double opacity, double luminosity, bool isLightMode)
        {
            if (!_isInitialized) return;

            bool isSolid = acrylicStyle.Equals("Solid", StringComparison.OrdinalIgnoreCase) || acrylicStyle.Equals("None", StringComparison.OrdinalIgnoreCase);

            if (isSolid)
            {
                ClearAcrylic();
                return;
            }

            bool isThin = acrylicStyle.Equals("AcrylicThin", StringComparison.OrdinalIgnoreCase);

            if (opacity <= 0) opacity = 0.65;
            if (luminosity <= 0) luminosity = 0.50;

            if (isThin) opacity = Math.Max(0.10, opacity - 0.35);

            byte alpha = (byte)(opacity * 255);
            uint gradientColor;

            if (isLightMode)
            {
                gradientColor = (uint)((alpha << 24) | (245 << 16) | (245 << 8) | 245);
            }
            else
            {
                byte rgb = (byte)(10 + (44 * luminosity));
                gradientColor = (uint)((alpha << 24) | (rgb << 16) | (rgb << 8) | rgb);
            }

            var policy = new Win32Helper.AccentPolicy
            {
                AccentState = EvolveOS_ShellEnhancer.Enums.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 2,
                GradientColor = gradientColor
            };

            ApplyAccentPolicy(policy);
        }

        public void ClearAcrylic()
        {
            var policy = new Win32Helper.AccentPolicy { AccentState = 0 };
            ApplyAccentPolicy(policy);
        }

        private void ApplyAccentPolicy(Win32Helper.AccentPolicy policy)
        {
            int policySize = Marshal.SizeOf(policy);
            IntPtr policyPtr = Marshal.AllocHGlobal(policySize);
            Marshal.StructureToPtr(policy, policyPtr, false);

            var data = new Win32Helper.WindowCompositionAttributeData
            {
                Attribute = 19,
                Data = policyPtr,
                SizeOfData = policySize
            };

            Win32Helper.SetWindowCompositionAttribute(_hWnd, ref data);
            Marshal.FreeHGlobal(policyPtr);
        }

        private void SetClassBrush(IntPtr hWnd, IntPtr brush)
        {
            if (IntPtr.Size == 8) SetClassLongPtr64(hWnd, -10, brush);
            else SetClassLong32(hWnd, -10, brush);
        }
    }
}