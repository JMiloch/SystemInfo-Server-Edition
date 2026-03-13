using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using Microsoft.Win32;

namespace SystemInfoApp
{
    public class DiskInfo
    {
        public string DriveLetter { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public long TotalSpace { get; set; }
        public long UsedSpace { get; set; }
        public long FreeSpace { get; set; }
        public DriveType DriveType { get; set; }

        public string GetFormattedDisk()
        {
            return $"{FormatBytes(UsedSpace)} / {FormatBytes(TotalSpace)}";
        }

        public int GetUsagePercent()
        {
            if (TotalSpace == 0) return 0;
            return (int)((UsedSpace * 100.0) / TotalSpace);
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class SystemInfo
    {
        public string ComputerName { get; set; } = string.Empty;
        public string DomainOrWorkgroup { get; set; } = string.Empty;
        public bool IsDomain { get; set; }
        public string OSCaption { get; set; } = string.Empty;
        public string OSDisplayVersion { get; set; } = string.Empty;  // e.g., "21H2"
        public string OSBuildFull { get; set; } = string.Empty;       // e.g., "10.0.20348.4171"
        public string LoggedInUser { get; set; } = string.Empty;
        public TimeSpan Uptime { get; set; }

        // CPU Information
        public string CPUName { get; set; } = string.Empty;
        public int CPUCores { get; set; }
        public int CPULogicalProcessors { get; set; }

        // RAM Information
        public long TotalRAM { get; set; }
        public long UsedRAM { get; set; }
        public long FreeRAM { get; set; }

        // All Disks
        public List<DiskInfo> Disks { get; set; } = new List<DiskInfo>();

        // Legacy - Disk Information (C:) for backwards compatibility
        public long TotalDiskSpace { get; set; }
        public long UsedDiskSpace { get; set; }
        public long FreeDiskSpace { get; set; }

        public static SystemInfo GetSystemInformation()
        {
            var info = new SystemInfo();

            try
            {
                // Computer Name
                info.ComputerName = Environment.MachineName;

                // Logged in User
                info.LoggedInUser = Environment.UserName;

                // Domain or Workgroup - using WMI for full FQDN
                try
                {
                    var csQuery = "SELECT PartOfDomain, Domain, Workgroup FROM Win32_ComputerSystem";
                    using var csSearcher = new ManagementObjectSearcher(csQuery);

                    foreach (ManagementObject cs in csSearcher.Get())
                    {
                        info.IsDomain = Convert.ToBoolean(cs["PartOfDomain"]);

                        if (info.IsDomain)
                        {
                            info.DomainOrWorkgroup = cs["Domain"]?.ToString() ?? "Unknown";
                        }
                        else
                        {
                            info.DomainOrWorkgroup = cs["Workgroup"]?.ToString() ?? "WORKGROUP";
                        }
                    }
                }
                catch
                {
                    // Fallback to Environment variables if WMI fails
                    info.IsDomain = Environment.UserDomainName != Environment.MachineName;
                    info.DomainOrWorkgroup = Environment.UserDomainName;
                }

                // OS Information via WMI (for Caption) and Registry (for version details)
                var query = "SELECT Caption, Version FROM Win32_OperatingSystem";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject os in searcher.Get())
                {
                    info.OSCaption = os["Caption"]?.ToString() ?? "Unknown";
                    // Get full version from WMI (e.g., "10.0.20348")
                    info.OSBuildFull = os["Version"]?.ToString() ?? "Unknown";
                }

                // Get DisplayVersion (e.g., "21H2") and UBR from Registry for full build number
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                    if (key != null)
                    {
                        info.OSDisplayVersion = key.GetValue("DisplayVersion")?.ToString() ?? "";

                        // Get UBR (Update Build Revision) to append to version
                        var ubr = key.GetValue("UBR");
                        if (ubr != null && !string.IsNullOrEmpty(info.OSBuildFull))
                        {
                            info.OSBuildFull = $"{info.OSBuildFull}.{ubr}";
                        }
                    }
                }
                catch { }

                // System Uptime
                info.Uptime = GetSystemUptime();

                // CPU Information via WMI
                GetCPUInformation(ref info);

                // RAM Information via WMI
                GetRAMInformation(ref info);

                // Disk Information for C:
                GetDiskInformation(ref info);

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting system info: {ex.Message}");
            }

            return info;
        }

        private static TimeSpan GetSystemUptime()
        {
            try
            {
                var query = "SELECT LastBootUpTime FROM Win32_OperatingSystem";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject os in searcher.Get())
                {
                    var lastBootTime = ManagementDateTimeConverter.ToDateTime(os["LastBootUpTime"].ToString());
                    return DateTime.Now - lastBootTime;
                }
            }
            catch { }

            return TimeSpan.Zero;
        }

        private static void GetCPUInformation(ref SystemInfo info)
        {
            try
            {
                var query = "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject cpu in searcher.Get())
                {
                    info.CPUName = cpu["Name"]?.ToString()?.Trim() ?? "Unknown";
                    info.CPUCores = Convert.ToInt32(cpu["NumberOfCores"]);
                    info.CPULogicalProcessors = Convert.ToInt32(cpu["NumberOfLogicalProcessors"]);
                    break; // Only need first CPU
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting CPU info: {ex.Message}");
            }
        }

        private static void GetRAMInformation(ref SystemInfo info)
        {
            try
            {
                var query = "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject os in searcher.Get())
                {
                    // Values are in KB, convert to bytes
                    info.TotalRAM = Convert.ToInt64(os["TotalVisibleMemorySize"]) * 1024;
                    info.FreeRAM = Convert.ToInt64(os["FreePhysicalMemory"]) * 1024;
                    info.UsedRAM = info.TotalRAM - info.FreeRAM;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting RAM info: {ex.Message}");
            }
        }

        private static void GetDiskInformation(ref SystemInfo info)
        {
            try
            {
                // Get all fixed and removable drives
                foreach (var drive in DriveInfo.GetDrives())
                {
                    // Only include ready drives that are Fixed or Removable
                    if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
                    {
                        var diskInfo = new DiskInfo
                        {
                            DriveLetter = drive.Name.TrimEnd('\\'),
                            VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                            TotalSpace = drive.TotalSize,
                            FreeSpace = drive.AvailableFreeSpace,
                            UsedSpace = drive.TotalSize - drive.AvailableFreeSpace,
                            DriveType = drive.DriveType
                        };
                        info.Disks.Add(diskInfo);

                        // Keep legacy C: info for backwards compatibility
                        if (drive.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                        {
                            info.TotalDiskSpace = drive.TotalSize;
                            info.FreeDiskSpace = drive.AvailableFreeSpace;
                            info.UsedDiskSpace = info.TotalDiskSpace - info.FreeDiskSpace;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting disk info: {ex.Message}");
            }
        }

        public string GetFormattedUptime()
        {
            if (Uptime.TotalDays >= 1)
                return $"{(int)Uptime.TotalDays}d {Uptime.Hours}h {Uptime.Minutes}m";
            else if (Uptime.TotalHours >= 1)
                return $"{Uptime.Hours}h {Uptime.Minutes}m";
            else
                return $"{Uptime.Minutes}m {Uptime.Seconds}s";
        }

        public string GetFormattedRAM()
        {
            return $"{FormatBytes(UsedRAM)} / {FormatBytes(TotalRAM)}";
        }

        public string GetFormattedDisk()
        {
            return $"{FormatBytes(UsedDiskSpace)} / {FormatBytes(TotalDiskSpace)}";
        }

        public int GetDiskUsagePercent()
        {
            if (TotalDiskSpace == 0) return 0;
            return (int)((UsedDiskSpace * 100.0) / TotalDiskSpace);
        }

        public int GetRAMUsagePercent()
        {
            if (TotalRAM == 0) return 0;
            return (int)((UsedRAM * 100.0) / TotalRAM);
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
