using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Simitone.Windows.GameLocator
{
    public class WindowsLocator : ILocator
    {
        public string FindTheSimsOnline()
        {
            return "";
            //string Software = "";

            //using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            //{
            //    //Find the path to TSO on the user's system.
            //    RegistryKey softwareKey = hklm.OpenSubKey("SOFTWARE");

            //    if (Array.Exists(softwareKey.GetSubKeyNames(), delegate (string s) { return s.Equals("Maxis", StringComparison.InvariantCultureIgnoreCase); }))
            //    {
            //        RegistryKey maxisKey = softwareKey.OpenSubKey("Maxis");
            //        if (Array.Exists(maxisKey.GetSubKeyNames(), delegate (string s) { return s.Equals("The Sims Online", StringComparison.InvariantCultureIgnoreCase); }))
            //        {
            //            RegistryKey tsoKey = maxisKey.OpenSubKey("The Sims Online");
            //            string installDir = (string)tsoKey.GetValue("InstallDir");
            //            installDir += "\\TSOClient\\";
            //            return installDir.Replace('\\', '/');
            //        }
            //    }
            //}
            //return @"C:\Program Files\Maxis\The Sims Online\TSOClient\".Replace('\\', '/');
        }

        public string FindTheSims1()
        {
            // Try each source in turn and keep the first one that holds the game. An old registry
            // entry from an uninstalled disc copy must not hide a Legacy Collection install.
            var candidates = new List<string>
            {
                // Relative directory, like macOS and Linux. Allows a portable setup.
                "../The Sims/",
                FindTheSimsRegistry(),
                FindTheSimsLegacySteam(),
                @"C:\Program Files (x86)\Maxis\The Sims\",
                @"C:\Program Files\Maxis\The Sims\"
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                if (File.Exists(Path.Combine(candidate, "GameData", "Behavior.iff")))
                    return Normalize(candidate);
            }

            // Nothing holds the game. Show the registry path if there is one, so the launcher
            // points at the folder the player most likely expects.
            return Normalize(candidates[1] ?? candidates[3]);
        }

        private static string Normalize(string path)
        {
            path = path.Replace('\\', '/');
            return path.EndsWith("/") ? path : path + "/";
        }

        private static string FindTheSimsRegistry()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var key = hklm.OpenSubKey(@"SOFTWARE\Maxis\The Sims"))
                {
                    var installDir = key?.GetValue("InstallPath") as string;
                    return string.IsNullOrWhiteSpace(installDir) ? null : installDir;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while reading The Sims registry path: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds The Sims Legacy Collection installed via Steam
        /// </summary>
        /// <param name="steamAppId">Steam App ID, default is The Sims: Legacy Collection</param>
        /// <returns>Full path if found, null otherwise</returns>
        private static string FindTheSimsLegacySteam(int steamAppId = 3314060)
        {
            try
            {
                return SteamGameLocator.GetGamePath(steamAppId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while finding The Sims: Legacy Collection Steam path: {ex.Message}");
                return null;
            }
        }

        private static bool is64BitProcess = (IntPtr.Size == 8);
        private static bool is64BitOperatingSystem = is64BitProcess || InternalCheckIsWow64();

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool wow64Process
        );

        /// <summary>
        /// Determines if this process is run on a 64bit OS.
        /// </summary>
        /// <returns>True if it is, false otherwise.</returns>
        public static bool InternalCheckIsWow64()
        {
            if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
                Environment.OSVersion.Version.Major >= 6)
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    bool retVal;
                    if (!IsWow64Process(p.Handle, out retVal))
                    {
                        return false;
                    }
                    return retVal;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
