// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UIAutomationClient;

namespace EvolveOS_ShellEnhancer.Utilities.Managers
{
    public static class NativeTrayOpener
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        public static void OpenNativeOverflow()
        {
            try
            {
                IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
                if (taskbarHwnd == IntPtr.Zero) return;

                CUIAutomation automation = new CUIAutomation();
                IUIAutomationElement taskbarElement = automation.ElementFromHandle(taskbarHwnd);
                if (taskbarElement == null) return;

                // Search for the Chevron using language-agnostic IDs
                IUIAutomationCondition id1 = automation.CreatePropertyCondition(30011, "SystemTrayChevron");
                IUIAutomationCondition id2 = automation.CreatePropertyCondition(30011, "1502");
                IUIAutomationCondition name1 = automation.CreatePropertyCondition(30005, "Show hidden icons");

                IUIAutomationCondition or1 = automation.CreateOrCondition(id1, id2);
                IUIAutomationCondition finalCondition = automation.CreateOrCondition(or1, name1);

                IUIAutomationElement chevronButton = taskbarElement.FindFirst(TreeScope.TreeScope_Descendants, finalCondition);

                if (chevronButton != null)
                {
                    // Programmatically trigger the button
                    var togglePattern = chevronButton.GetCurrentPattern(10015) as IUIAutomationTogglePattern;
                    if (togglePattern != null)
                    {
                        togglePattern.Toggle();
                    }
                    else
                    {
                        var invokePattern = chevronButton.GetCurrentPattern(10000) as IUIAutomationInvokePattern;
                        invokePattern?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open tray overflow: {ex.Message}");
            }
        }
    }
}