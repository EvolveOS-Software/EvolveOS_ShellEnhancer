// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.
using System.Runtime.InteropServices;

namespace EvolveOS_ShellEnhancer.Utilities.Managers
{
    public static class KeyboardHookManager
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static event Action? WindowsKeyPressed;

        public static void StartHook()
        {
            if (_hookID == IntPtr.Zero)
            {
                _hookID = SetHook(_proc);
            }
        }

        public static void StopHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule!.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                if (Win32Helper.IsSimulating)
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                int vkCode = Marshal.ReadInt32(lParam);

                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                {
                    WindowsKeyPressed?.Invoke();

                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        #region Native Interop
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion
    }
}