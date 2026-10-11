// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace EvolveOS_ShellEnhancer.Utilities.Managers
{
    public static class KeyboardHookManager
    {
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static event Action? WindowsKeyPressed;

        private static bool _isWinKeyDown = false;

        private static bool _wasOtherKeyPressedWithWin = false;

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
                if (curModule != null)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
                return IntPtr.Zero;
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (Win32Helper.IsSimulating)
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                int vkCode = Marshal.ReadInt32(lParam);
                bool isKeyDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);
                bool isKeyUp = (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP);

                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                {
                    if (isKeyDown)
                    {
                        if (!_isWinKeyDown)
                        {
                            _isWinKeyDown = true;
                            _wasOtherKeyPressedWithWin = false;
                        }
                    }
                    else if (isKeyUp)
                    {
                        if (_isWinKeyDown && !_wasOtherKeyPressedWithWin)
                        {
                            WindowsKeyPressed?.Invoke();

                            _isWinKeyDown = false;
                            return (IntPtr)1;
                        }

                        _isWinKeyDown = false;
                    }
                }
                else
                {
                    if (isKeyDown && _isWinKeyDown)
                    {
                        _wasOtherKeyPressedWithWin = true;
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}