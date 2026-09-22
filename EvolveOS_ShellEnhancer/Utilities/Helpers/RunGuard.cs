// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    internal static class RunGuard
    {
        private static Mutex? _appMutex;

        #region OS Version & Build Info Helpers (Registry UBR Enabled)

        private static (int Build, int Revision) GetDetailedOSVersion()
        {
            int build = Environment.OSVersion.Version.Build;
            int revision = 0;

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (key != null)
                    {
                        object? ubrObj = key.GetValue("UBR");
                        if (ubrObj is int ubrVal)
                        {
                            revision = ubrVal;
                        }

                        object? buildObj = key.GetValue("CurrentBuildNumber") ?? key.GetValue("CurrentBuild");
                        if (buildObj != null && int.TryParse(buildObj.ToString(), out int regBuild))
                        {
                            build = regBuild;
                        }
                    }
                }
            }
            catch
            {
                revision = Math.Max(0, Environment.OSVersion.Version.Revision);
            }

            return (build, revision);
        }

        private static bool IsWindows11()
        {
            var (build, _) = GetDetailedOSVersion();
            return build >= 22000;
        }

        private static bool IsWindows10()
        {
            var (build, _) = GetDetailedOSVersion();
            return build >= 10240 && build < 22000;
        }

        #endregion

        #region Startup Environment & System Requirements Validation

        internal static bool ValidateStartupEnvironment()
        {
            _appMutex = new Mutex(true, "EvolveOS_ShellEnhancer_Unique_Instance_Mutex", out bool isFirstInstance);

            if (!isFirstInstance)
            {
                var runningMsgWindow = new MessageWindow(MessageWindowState.AlreadyRunning);
                runningMsgWindow.Activate();
                return false;
            }

            string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            string optimizerPath = Path.Combine(exeDir, "EvolveOS_Optimizer.exe");

            if (!File.Exists(optimizerPath))
            {
                var msgWindow = new MessageWindow(MessageWindowState.MissingOptimizer);
                msgWindow.Activate();
                return false;
            }

            var (buildNumber, buildRevision) = GetDetailedOSVersion();

            bool isSupported = (buildNumber > 26200) || (buildNumber == 26200 && buildRevision >= 9278);

            if (!isSupported)
            {
                var msgWindow = new MessageWindow(MessageWindowState.NotSupported);
                msgWindow.Activate();
                return false;
            }

            return true;
        }

        #endregion

        #region Windows Versioning & Compatibility Filter

        internal static bool IsOSVersionCompatible(
            int minimumBuild,
            int? maximumBuild = null,
            int? minimumRevision = null,
            int? maximumRevision = null,
            bool isWindows10Only = false,
            bool isWindows11Only = false,
            int[]? supportedBuildRanges = null)
        {
            try
            {
                bool isWin11 = IsWindows11();
                var (buildNumber, buildRevision) = GetDetailedOSVersion();

                if (isWindows10Only && isWin11) return false;
                if (isWindows11Only && !isWin11) return false;

                if (supportedBuildRanges != null && supportedBuildRanges.Length >= 2)
                {
                    bool inRange = false;
                    for (int i = 0; i < supportedBuildRanges.Length; i += 2)
                    {
                        int min = supportedBuildRanges[i];
                        int max = supportedBuildRanges[i + 1];
                        if (buildNumber >= min && buildNumber <= max)
                        {
                            inRange = true;
                            break;
                        }
                    }
                    if (!inRange) return false;
                }

                if (buildNumber < minimumBuild) return false;
                if (buildNumber == minimumBuild && minimumRevision.HasValue && buildRevision < minimumRevision.Value) return false;

                if (maximumBuild.HasValue)
                {
                    if (buildNumber > maximumBuild.Value) return false;
                    if (buildNumber == maximumBuild.Value && maximumRevision.HasValue && buildRevision > maximumRevision.Value) return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RunGuard] Version compatibility evaluation failed: {ex.Message}");
                return true;
            }
        }

        #endregion
    }
}