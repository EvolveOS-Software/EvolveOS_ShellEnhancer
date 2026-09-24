// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.IO;

namespace EvolveOS_ShellEnhancer.Utilities.Services
{
    internal static class PathLocator
    {
        #region Executable
        internal static class Executable
        {
            private static readonly ConcurrentDictionary<string, string> exeCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            internal static string FindExecutablePath(params string[] names)
            {
                if (names == null || names.Length == 0)
                {
                    return string.Empty;
                }

                for (int ni = 0; ni < names.Length; ni++)
                {
                    string name = names[ni];
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (exeCache.TryGetValue(name, out string? cachedValue))
                    {
                        if (!string.IsNullOrEmpty(cachedValue))
                        {
                            return cachedValue;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (Path.IsPathRooted(name))
                    {
                        string absolute = File.Exists(name) ? name : string.Empty;
                        exeCache.TryAdd(name, absolute);
                        if (!string.IsNullOrEmpty(absolute))
                        {
                            return absolute;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    string pathextRaw = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE";

                    string[] pathextEntries = pathextRaw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int pei = 0; pei < pathextEntries.Length; pei++)
                    {
                        string e = pathextEntries[pei].Trim().Trim('"');
                        if (string.IsNullOrEmpty(e))
                        {
                            e = ".EXE";
                        }

                        if (e[0] != '.')
                        {
                            e = "." + e;
                        }

                        pathextEntries[pei] = e;
                    }

                    string[] namesToTry;
                    if (Path.HasExtension(name))
                    {
                        namesToTry = new[] { name };
                    }
                    else
                    {
                        List<string> tmp = new List<string>(pathextEntries.Length);
                        for (int pei = 0; pei < pathextEntries.Length; pei++)
                        {
                            tmp.Add(name + pathextEntries[pei]);
                        }

                        namesToTry = tmp.ToArray();
                    }

                    if (name.IndexOf(Path.DirectorySeparatorChar) >= 0 || name.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                    {
                        string current = Directory.GetCurrentDirectory();
                        for (int ti = 0; ti < namesToTry.Length; ti++)
                        {
                            string candidateRel = Path.Combine(current, namesToTry[ti]);
                            try
                            {
                                if (File.Exists(candidateRel))
                                {
                                    exeCache.TryAdd(name, candidateRel);
                                    return candidateRel;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }
                        }
                    }

                    HashSet<string> searchDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    string[] pathEntries = pathEnv.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
                    for (int pe = 0; pe < pathEntries.Length; pe++)
                    {
                        string entry = pathEntries[pe].Trim().Trim('"');
                        if (!string.IsNullOrEmpty(entry))
                        {
                            searchDirs.Add(entry);
                        }
                    }

                    string systemDir = Environment.SystemDirectory ?? "";
                    if (!string.IsNullOrEmpty(systemDir))
                    {
                        searchDirs.Add(systemDir);
                    }

                    string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows) ?? "";
                    if (!string.IsNullOrEmpty(windows))
                    {
                        searchDirs.Add(Path.Combine(windows, "System32"));
                        searchDirs.Add(Path.Combine(windows, "Sysnative"));
                        searchDirs.Add(Path.Combine(windows, "SysWOW64"));
                    }

                    bool found = false;
                    string foundPath = string.Empty;
                    foreach (string dir in searchDirs)
                    {
                        try
                        {
                            if (!Directory.Exists(dir))
                            {
                                continue;
                            }

                            for (int ti = 0; ti < namesToTry.Length; ti++)
                            {
                                string candidate = Path.Combine(dir, namesToTry[ti]);
                                try
                                {
                                    if (File.Exists(candidate))
                                    {
                                        found = true;
                                        foundPath = candidate;
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex.Message);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }

                        if (found)
                        {
                            break;
                        }
                    }

                    if (found)
                    {
                        exeCache.TryAdd(name, foundPath);
                        return foundPath;
                    }

                    string[] programRoots = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) ?? "",
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) ?? ""
                    };

                    for (int r = 0; r < programRoots.Length; r++)
                    {
                        string root = programRoots[r];
                        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                        {
                            continue;
                        }

                        try
                        {
                            string[] candidateDirs = Directory.GetDirectories(root, "PowerShell*", SearchOption.TopDirectoryOnly);
                            for (int d = 0; d < candidateDirs.Length; d++)
                            {
                                string candidateDir = candidateDirs[d];
                                for (int ti = 0; ti < namesToTry.Length; ti++)
                                {
                                    string candidate = Path.Combine(candidateDir, namesToTry[ti]);
                                    try
                                    {
                                        if (File.Exists(candidate))
                                        {
                                            exeCache.TryAdd(name, candidate);
                                            return candidate;
                                        }
                                    }
                                    catch (Exception ex) { Debug.WriteLine(ex.Message); }
                                }
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine(ex.Message); }
                    }

                    exeCache.TryAdd(name, string.Empty);
                }

                return string.Empty;
            }

            internal static readonly string CommandShell = FindExecutablePath("cmd.exe");

            internal static readonly string PowerShell = FindExecutablePath("pwsh.exe", "powershell.exe");
        }
        #endregion
    }
}