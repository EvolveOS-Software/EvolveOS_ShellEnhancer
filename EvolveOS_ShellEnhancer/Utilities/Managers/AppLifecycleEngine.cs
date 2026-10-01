// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace EvolveOS_ShellEnhancer.Managers
{
    public static class AppLifecycleEngine
    {
        #region Properties
        private static readonly HashSet<string> _wakeLocks = new();
        private static readonly object _lockObj = new();
        private static bool _isCurrentlySuspended = false;

        public static bool IsEfficiencyModeEnabled { get; set; } = false;
        #endregion

        public static void RegisterWakeLock(string componentName)
        {
            lock (_lockObj)
            {
                _wakeLocks.Add(componentName);

                if (_isCurrentlySuspended)
                {
                    SetEfficiencyMode(false);
                }
            }
        }

        public static void ReleaseWakeLock(string componentName)
        {
            lock (_lockObj)
            {
                _wakeLocks.Remove(componentName);

                if (_wakeLocks.Count == 0 && !_isCurrentlySuspended)
                {
                    SetEfficiencyMode(true);
                }
            }
        }

        private static void SetEfficiencyMode(bool enable)
        {
            if (enable && !IsEfficiencyModeEnabled)
                return;

            IntPtr hProcess = IntPtr.Zero;
            try
            {
                int pid = Process.GetCurrentProcess().Id;
                hProcess = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_SET_QUOTA, false, pid);

                if (hProcess == IntPtr.Zero) return;

                uint priorityClass = enable ? IDLE_PRIORITY_CLASS : NORMAL_PRIORITY_CLASS;
                SetPriorityClass(hProcess, priorityClass);

                var state = new PROCESS_POWER_THROTTLING_STATE
                {
                    Version = 1,
                    ControlMask = 1u,
                    StateMask = enable ? 1u : 0u
                };
                SetProcessInformation(hProcess, 4, ref state, (uint)Marshal.SizeOf(state));

                if (enable)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    SetProcessWorkingSetSize(hProcess, (IntPtr)(-1), (IntPtr)(-1));
                    Debug.WriteLine("[EcoQoS] App Suspended: RAM Trimmed.");
                }
                else
                {
                    Debug.WriteLine("[EcoQoS] App Awakened: Normal Priority Restored.");
                }

                _isCurrentlySuspended = enable;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EcoQoS] Exception: {ex.Message}");
            }
            finally
            {
                if (hProcess != IntPtr.Zero) CloseHandle(hProcess);
            }
        }
    }
}