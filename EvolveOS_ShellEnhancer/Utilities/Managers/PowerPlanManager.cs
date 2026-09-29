// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace EvolveOS_ShellEnhancer.Utilities.Managers
{
    public static class PowerPlanManager
    {
        public static List<PowerPlan> GetPowerPlans()
        {
            var plans = new List<PowerPlan>();

            try
            {
                Guid activePlanId = GetActivePlanId();
                uint index = 0;
                uint bufferSize = (uint)Marshal.SizeOf(typeof(Guid));

                while (true)
                {
                    uint result = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ACCESS_SCHEME, index, out Guid planGuid, ref bufferSize);

                    if (result == ERROR_NO_MORE_ITEMS)
                        break;

                    if (result == ERROR_SUCCESS)
                    {
                        plans.Add(new PowerPlan
                        {
                            Id = planGuid,
                            Name = ReadFriendlyName(planGuid),
                            IsActive = (planGuid == activePlanId)
                        });
                    }

                    index++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to enumerate power plans: {ex.Message}");
            }

            return plans;
        }

        public static bool SetActivePlan(Guid planId)
        {
            try
            {
                uint result = PowerSetActiveScheme(IntPtr.Zero, ref planId);
                return result == ERROR_SUCCESS;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set active power plan: {ex.Message}");
                return false;
            }
        }

        private static Guid GetActivePlanId()
        {
            IntPtr pActiveSchemeGuid = IntPtr.Zero;
            try
            {
                uint result = PowerGetActiveScheme(IntPtr.Zero, out pActiveSchemeGuid);
                if (result == ERROR_SUCCESS && pActiveSchemeGuid != IntPtr.Zero)
                {
                    return Marshal.PtrToStructure<Guid>(pActiveSchemeGuid);
                }
            }
            finally
            {
                if (pActiveSchemeGuid != IntPtr.Zero)
                {
                    LocalFree(pActiveSchemeGuid);
                }
            }
            return Guid.Empty;
        }

        private static string ReadFriendlyName(Guid schemeGuid)
        {
            uint bufferSize = 0;

            PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bufferSize);

            if (bufferSize == 0) return "Unknown Plan";

            IntPtr pBuffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                uint result = PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pBuffer, ref bufferSize);
                if (result == ERROR_SUCCESS)
                {
                    return Marshal.PtrToStringUni(pBuffer) ?? "Unknown Plan";
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pBuffer);
            }

            return "Unknown Plan";
        }
    }
}