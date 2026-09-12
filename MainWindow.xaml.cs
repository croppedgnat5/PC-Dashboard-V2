using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;
using System.Windows.Controls;

namespace PCDashboard
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _refreshTimer;

        private readonly Dictionary<int, TimeSpan> _previousCpuTimes = new();

        private DateTime _previousSampleTime;

        private int _refreshIntervalSeconds = 2;

        public MainWindow()
        {
            InitializeComponent();

            _previousSampleTime = DateTime.UtcNow;

            LoadProcesses();
            LoadHardware();
            LoadDisks();

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(
                    _refreshIntervalSeconds)
            };

            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void RefreshTimer_Tick(
            object? sender,
            EventArgs e)
        {
            LoadProcesses();
            LoadHardware();
        }

        private void HideAllPages()
        {
            HomePage.Visibility = Visibility.Collapsed;
            HardwarePage.Visibility = Visibility.Collapsed;
            DiskPage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Collapsed;

            HomeButton.Background = Brushes.Transparent;
            HardwareButton.Background = Brushes.Transparent;
            DiskButton.Background = Brushes.Transparent;
            SettingsButton.Background = Brushes.Transparent;

            HomeButton.Foreground =
                (Brush)Resources["SecondaryText"];

            HardwareButton.Foreground =
                (Brush)Resources["SecondaryText"];

            DiskButton.Foreground =
                (Brush)Resources["SecondaryText"];

            SettingsButton.Foreground =
                (Brush)Resources["SecondaryText"];
        }

        private void HomeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideAllPages();

            HomePage.Visibility =
                Visibility.Visible;

            HomeButton.Background =
                (Brush)Resources["SelectedButtonBackground"];

            HomeButton.Foreground =
                (Brush)Resources["PrimaryText"];
        }

        private void HardwareButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideAllPages();

            HardwarePage.Visibility =
                Visibility.Visible;

            HardwareButton.Background =
                (Brush)Resources["SelectedButtonBackground"];

            HardwareButton.Foreground =
                (Brush)Resources["PrimaryText"];

            LoadHardware();
        }

        private void DiskButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideAllPages();

            DiskPage.Visibility =
                Visibility.Visible;

            DiskButton.Background =
                (Brush)Resources["SelectedButtonBackground"];

            DiskButton.Foreground =
                (Brush)Resources["PrimaryText"];

            LoadDisks();
        }

        private void SettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HideAllPages();

            SettingsPage.Visibility =
                Visibility.Visible;

            SettingsButton.Background =
                (Brush)Resources["SelectedButtonBackground"];

            SettingsButton.Foreground =
                (Brush)Resources["PrimaryText"];

            // Keep the controls synchronized
            // with the current refresh interval.
            switch (_refreshIntervalSeconds)
            {
                case 1:
                    SettingsRefreshComboBox.SelectedIndex = 0;
                    break;

                case 2:
                    SettingsRefreshComboBox.SelectedIndex = 1;
                    break;

                case 5:
                    SettingsRefreshComboBox.SelectedIndex = 2;
                    break;

                case 10:
                    SettingsRefreshComboBox.SelectedIndex = 3;
                    break;

                default:
                    SettingsRefreshComboBox.SelectedIndex = 1;
                    break;
            }
        }

        // ============================================================
        // SETTINGS
        // ============================================================

        private void ApplySettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Refresh interval
            switch (SettingsRefreshComboBox.SelectedIndex)
            {
                case 0:
                    _refreshIntervalSeconds = 1;
                    break;

                case 1:
                    _refreshIntervalSeconds = 2;
                    break;

                case 2:
                    _refreshIntervalSeconds = 5;
                    break;

                case 3:
                    _refreshIntervalSeconds = 10;
                    break;

                default:
                    _refreshIntervalSeconds = 2;
                    break;
            }

            _refreshTimer.Interval =
                TimeSpan.FromSeconds(
                    _refreshIntervalSeconds);

            SettingsStatusText.Text =
                $"Settings applied. Refreshing every {_refreshIntervalSeconds} second{(_refreshIntervalSeconds == 1 ? "" : "s")}.";

            // Startup page selection
            switch (SettingsStartupPageComboBox.SelectedIndex)
            {
                case 0:
                    HomeButton_Click(
                        HomeButton,
                        new RoutedEventArgs());
                    break;

                case 1:
                    HardwareButton_Click(
                        HardwareButton,
                        new RoutedEventArgs());
                    break;

                case 2:
                    DiskButton_Click(
                        DiskButton,
                        new RoutedEventArgs());
                    break;
            }
        }

        private void ResetSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _refreshIntervalSeconds = 2;

            SettingsRefreshComboBox.SelectedIndex = 1;

            SettingsStartupPageComboBox.SelectedIndex = 0;

            _refreshTimer.Interval =
                TimeSpan.FromSeconds(2);

            SettingsStatusText.Text =
                "Settings have been reset to their default values.";

            HomeButton_Click(
                HomeButton,
                new RoutedEventArgs());
        }

        // ============================================================
        // PROCESS MONITOR
        // ============================================================

        private void LoadProcesses()
        {
            try
            {
                DateTime currentSampleTime =
                    DateTime.UtcNow;

                double elapsedSeconds =
                    (currentSampleTime - _previousSampleTime)
                    .TotalSeconds;

                Process[] processes =
                    Process.GetProcesses();

                var processInfos =
                    new List<ProcessInfo>();

                var currentCpuTimes =
                    new Dictionary<int, TimeSpan>();

                int processorCount =
                    Environment.ProcessorCount;

                foreach (Process process in processes)
                {
                    try
                    {
                        TimeSpan currentCpuTime =
                            process.TotalProcessorTime;

                        currentCpuTimes[process.Id] =
                            currentCpuTime;

                        string cpuUsage = "N/A";

                        double cpuValue = -1;

                        if (_previousCpuTimes.TryGetValue(
                                process.Id,
                                out TimeSpan previousCpuTime)
                            && elapsedSeconds > 0)
                        {
                            double cpuTimeUsed =
                                (currentCpuTime - previousCpuTime)
                                .TotalSeconds;

                            cpuValue =
                                cpuTimeUsed /
                                elapsedSeconds /
                                processorCount *
                                100.0;

                            if (cpuValue < 0)
                            {
                                cpuValue = 0;
                            }

                            if (cpuValue > 100)
                            {
                                cpuValue = 100;
                            }

                            cpuUsage =
                                $"{cpuValue:0.0}%";
                        }

                        processInfos.Add(
                            new ProcessInfo
                            {
                                Name =
                                    process.ProcessName,

                                Id =
                                    process.Id,

                                CpuUsage =
                                    cpuUsage,

                                CpuValue =
                                    cpuValue,

                                MemoryUsage =
                                    FormatMemory(
                                        process.WorkingSet64),

                                MemoryBytes =
                                    process.WorkingSet64
                            });
                    }
                    catch
                    {
                        // Some Windows processes are protected.
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                _previousCpuTimes.Clear();

                foreach (var entry in currentCpuTimes)
                {
                    _previousCpuTimes[entry.Key] =
                        entry.Value;
                }

                _previousSampleTime =
                    currentSampleTime;

                ApplySorting(processInfos);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Process monitor error: {ex.Message}");
            }
        }

        private void ApplySorting(
            List<ProcessInfo> processes)
        {
            int selectedIndex =
                SortComboBox?.SelectedIndex ?? 0;

            IEnumerable<ProcessInfo> sortedProcesses;

            switch (selectedIndex)
            {
                case 1:
                    sortedProcesses =
                        processes.OrderBy(
                            p => p.CpuValue);
                    break;

                case 2:
                    sortedProcesses =
                        processes.OrderByDescending(
                            p => p.MemoryBytes);
                    break;

                case 3:
                    sortedProcesses =
                        processes.OrderBy(
                            p => p.MemoryBytes);
                    break;

                case 4:
                    sortedProcesses =
                        processes.OrderBy(
                            p => p.Name,
                            StringComparer.OrdinalIgnoreCase);
                    break;

                case 5:
                    sortedProcesses =
                        processes.OrderByDescending(
                            p => p.Name,
                            StringComparer.OrdinalIgnoreCase);
                    break;

                case 6:
                    sortedProcesses =
                        processes.OrderBy(
                            p => p.Id);
                    break;

                case 7:
                    sortedProcesses =
                        processes.OrderByDescending(
                            p => p.Id);
                    break;

                default:
                    sortedProcesses =
                        processes.OrderByDescending(
                            p => p.CpuValue);
                    break;
            }

            ProcessDataGrid.ItemsSource =
                sortedProcesses.ToList();
        }

        private void SortComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
            {
                return;
            }

            LoadProcesses();
        }

        private void RefreshProcessesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoadProcesses();
        }

        private string FormatMemory(long bytes)
        {
            double megabytes =
                bytes / 1024.0 / 1024.0;

            if (megabytes >= 1024)
            {
                return $"{megabytes / 1024:0.0} GB";
            }

            return $"{megabytes:0} MB";
        }

        // ============================================================
        // HARDWARE INFORMATION
        // ============================================================

        private void LoadHardware()
        {
            try
            {
                LoadCpuInformation();
                LoadMemoryInformation();
                LoadGpuInformation();
                LoadMotherboardInformation();
                LoadSystemInformation();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Hardware monitor error: {ex.Message}");
            }
        }

        private void LoadCpuInformation()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");

                foreach (ManagementObject cpu in searcher.Get())
                {
                    HardwareCpuName.Text =
                        cpu["Name"]?.ToString()?.Trim()
                        ?? "Unknown CPU";

                    HardwareCpuCores.Text =
                        $"{cpu["NumberOfCores"] ?? "N/A"} Cores";

                    HardwareCpuThreads.Text =
                        $"{cpu["NumberOfLogicalProcessors"] ?? "N/A"} Threads";

                    break;
                }
            }
            catch
            {
                HardwareCpuName.Text =
                    "Unable to read CPU";

                HardwareCpuCores.Text =
                    "N/A";

                HardwareCpuThreads.Text =
                    "N/A";
            }
        }

        private void LoadMemoryInformation()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

                foreach (ManagementObject memory in searcher.Get())
                {
                    ulong totalKb =
                        Convert.ToUInt64(
                            memory["TotalVisibleMemorySize"]);

                    ulong freeKb =
                        Convert.ToUInt64(
                            memory["FreePhysicalMemory"]);

                    double totalGb =
                        totalKb / 1024.0 / 1024.0;

                    double freeGb =
                        freeKb / 1024.0 / 1024.0;

                    double usedGb =
                        totalGb - freeGb;

                    double usage =
                        usedGb / totalGb * 100.0;

                    HardwareRamTotal.Text =
                        $"{totalGb:0.0} GB";

                    HardwareRamUsed.Text =
                        $"{usedGb:0.0} GB";

                    HardwareRamAvailable.Text =
                        $"{freeGb:0.0} GB";

                    HardwareRamUsage.Text =
                        $"{usage:0}%";

                    break;
                }
            }
            catch
            {
                HardwareRamTotal.Text = "N/A";
                HardwareRamUsed.Text = "N/A";
                HardwareRamAvailable.Text = "N/A";
                HardwareRamUsage.Text = "N/A";
            }
        }

        private void LoadGpuInformation()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Name, AdapterRAM FROM Win32_VideoController");

                foreach (ManagementObject gpu in searcher.Get())
                {
                    string gpuName =
                        gpu["Name"]?.ToString()
                        ?? "Unknown GPU";

                    HardwareGpuName.Text =
                        gpuName;

                    string vram =
                        GetKnownGpuVram(gpuName);

                    HardwareGpuVram.Text =
                        vram;

                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"GPU detection error: {ex.Message}");

                HardwareGpuName.Text =
                    "Unknown GPU";

                HardwareGpuVram.Text =
                    "Unknown";
            }
        }

        private string GetKnownGpuVram(
            string gpuName)
        {
            string name =
                gpuName.ToUpperInvariant();

            // NVIDIA RTX 40-series

            if (name.Contains("RTX 4090"))
                return "24 GB";

            if (name.Contains("RTX 4080 SUPER") ||
                name.Contains("RTX 4080"))
                return "16 GB";

            if (name.Contains("RTX 4070 TI SUPER"))
                return "16 GB";

            if (name.Contains("RTX 4070 TI"))
                return "12 GB";

            if (name.Contains("RTX 4070 SUPER") ||
                name.Contains("RTX 4070"))
                return "12 GB";

            if (name.Contains("RTX 4060 TI"))
                return "8 GB";

            if (name.Contains("RTX 4060"))
                return "8 GB";

            // NVIDIA RTX 30-series

            if (name.Contains("RTX 3090"))
                return "24 GB";

            if (name.Contains("RTX 3080"))
                return "10 GB";

            if (name.Contains("RTX 3070"))
                return "8 GB";

            if (name.Contains("RTX 3060"))
                return "12 GB";

            return GetWmiVram();
        }

        private string GetWmiVram()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT AdapterRAM FROM Win32_VideoController");

                foreach (ManagementObject gpu in searcher.Get())
                {
                    if (gpu["AdapterRAM"] == null)
                        continue;

                    ulong bytes =
                        Convert.ToUInt64(
                            gpu["AdapterRAM"]);

                    double gb =
                        bytes /
                        1024.0 /
                        1024.0 /
                        1024.0;

                    return $"{gb:0.0} GB";
                }
            }
            catch
            {
            }

            return "Unknown";
        }

        private double? GetGpuVramFromRegistry(
            string deviceId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                    return null;

                string registryPath =
                    @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

                using Microsoft.Win32.RegistryKey? displayClass =
                    Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        registryPath);

                if (displayClass == null)
                    return null;

                foreach (string subKeyName in
                    displayClass.GetSubKeyNames())
                {
                    if (!int.TryParse(
                            subKeyName,
                            out _))
                    {
                        continue;
                    }

                    using Microsoft.Win32.RegistryKey? gpuKey =
                        displayClass.OpenSubKey(
                            subKeyName);

                    if (gpuKey == null)
                        continue;

                    string? registryDeviceId =
                        gpuKey.GetValue(
                            "MatchingDeviceId")
                        ?.ToString();

                    if (string.IsNullOrEmpty(
                            registryDeviceId))
                    {
                        registryDeviceId =
                            gpuKey.GetValue(
                                "DeviceInstanceID")
                            ?.ToString();
                    }

                    if (string.IsNullOrEmpty(
                            registryDeviceId))
                    {
                        continue;
                    }

                    if (!deviceId.Contains(
                            registryDeviceId,
                            StringComparison.OrdinalIgnoreCase)
                        &&
                        !registryDeviceId.Contains(
                            deviceId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    object? memoryValue =
                        gpuKey.GetValue(
                            "HardwareInformation.MemorySize");

                    if (memoryValue == null)
                        continue;

                    ulong memoryBytes;

                    if (memoryValue is byte[] bytes)
                    {
                        if (bytes.Length >= 8)
                        {
                            memoryBytes =
                                BitConverter.ToUInt64(
                                    bytes,
                                    0);
                        }
                        else if (bytes.Length >= 4)
                        {
                            memoryBytes =
                                BitConverter.ToUInt32(
                                    bytes,
                                    0);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        memoryBytes =
                            Convert.ToUInt64(
                                memoryValue);
                    }

                    double gigabytes =
                        memoryBytes /
                        1024.0 /
                        1024.0 /
                        1024.0;

                    if (gigabytes > 0)
                        return gigabytes;
                }
            }
            catch
            {
                // Ignore registry access errors.
            }

            return null;
        }

        private void LoadMotherboardInformation()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Manufacturer, Product FROM Win32_BaseBoard");

                foreach (ManagementObject board in searcher.Get())
                {
                    string manufacturer =
                        board["Manufacturer"]?.ToString()
                        ?? "";

                    string product =
                        board["Product"]?.ToString()
                        ?? "";

                    HardwareMotherboard.Text =
                        $"{manufacturer} {product}".Trim();

                    break;
                }
            }
            catch
            {
                HardwareMotherboard.Text =
                    "Unable to read motherboard";
            }
        }

        private void LoadSystemInformation()
        {
            try
            {
                HardwareWindows.Text =
                    Environment.OSVersion.VersionString;

                HardwareArchitecture.Text =
                    Environment.Is64BitOperatingSystem
                        ? "64-bit"
                        : "32-bit";

                HardwareProcessorCount.Text =
                    $"{Environment.ProcessorCount} Logical Processors";
            }
            catch
            {
                HardwareWindows.Text = "N/A";
                HardwareArchitecture.Text = "N/A";
                HardwareProcessorCount.Text = "N/A";
            }
        }

        // ============================================================
        // DISK ANALYZER
        // ============================================================

        private void LoadDisks()
        {
            try
            {
                DiskCardsPanel.Children.Clear();

                Dictionary<string, PhysicalDiskInfo> physicalDisks =
                    GetPhysicalDiskInformation();

                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!drive.IsReady)
                            continue;

                        long totalBytes =
                            drive.TotalSize;

                        long freeBytes =
                            drive.AvailableFreeSpace;

                        long usedBytes =
                            totalBytes - freeBytes;

                        double usagePercentage =
                            totalBytes > 0
                                ? (double)usedBytes /
                                  totalBytes *
                                  100
                                : 0;

                        string driveLetter =
                            drive.Name.TrimEnd('\\');

                        PhysicalDiskInfo diskInfo =
                            FindPhysicalDiskForDrive(
                                driveLetter,
                                physicalDisks);

                        Border card =
                            new Border
                            {
                                Width = 360,

                                Margin =
                                    new Thickness(
                                        0,
                                        0,
                                        15,
                                        15),

                                Padding =
                                    new Thickness(
                                        20),

                                Background =
                                    (Brush)Resources[
                                        "PanelBackground"],

                                BorderBrush =
                                    (Brush)Resources[
                                        "PanelBorder"],

                                BorderThickness =
                                    new Thickness(1),

                                CornerRadius =
                                    new CornerRadius(8)
                            };

                        StackPanel content =
                            new StackPanel();

                        string volumeLabel =
                            string.IsNullOrWhiteSpace(
                                drive.VolumeLabel)
                                ? "Local Drive"
                                : drive.VolumeLabel;

                        TextBlock driveName =
                            new TextBlock
                            {
                                Text =
                                    $"{driveLetter}  •  {volumeLabel}",

                                Foreground =
                                    (Brush)Resources[
                                        "PrimaryText"],

                                FontSize = 18,

                                FontWeight =
                                    FontWeights.SemiBold
                            };

                        content.Children.Add(
                            driveName);

                        TextBlock modelText =
                            new TextBlock
                            {
                                Text =
                                    diskInfo.Model,

                                Foreground =
                                    (Brush)Resources[
                                        "PrimaryText"],

                                FontSize = 13,

                                Margin =
                                    new Thickness(
                                        0,
                                        8,
                                        0,
                                        0),

                                TextWrapping =
                                    TextWrapping.Wrap
                            };

                        content.Children.Add(
                            modelText);

                        TextBlock typeText =
                            new TextBlock
                            {
                                Text =
                                    $"{diskInfo.DriveType}  •  {diskInfo.Interface}",

                                Foreground =
                                    (Brush)Resources[
                                        "AccentText"],

                                FontSize = 12,

                                Margin =
                                    new Thickness(
                                        0,
                                        4,
                                        0,
                                        0)
                            };

                        content.Children.Add(
                            typeText);

                        TextBlock fileSystem =
                            new TextBlock
                            {
                                Text =
                                    $"File System: {drive.DriveFormat}",

                                Foreground =
                                    (Brush)Resources[
                                        "SecondaryText"],

                                FontSize = 12,

                                Margin =
                                    new Thickness(
                                        0,
                                        4,
                                        0,
                                        15)
                            };

                        content.Children.Add(
                            fileSystem);

                        TextBlock usageText =
                            new TextBlock
                            {
                                Text =
                                    $"{FormatStorage(usedBytes)} used of {FormatStorage(totalBytes)}",

                                Foreground =
                                    (Brush)Resources[
                                        "PrimaryText"],

                                FontSize = 13
                            };

                        content.Children.Add(
                            usageText);

                        ProgressBar progressBar =
                            new ProgressBar
                            {
                                Minimum = 0,

                                Maximum = 100,

                                Value =
                                    usagePercentage,

                                Height = 10,

                                Margin =
                                    new Thickness(
                                        0,
                                        8,
                                        0,
                                        8)
                            };

                        content.Children.Add(
                            progressBar);

                        TextBlock freeText =
                            new TextBlock
                            {
                                Text =
                                    $"{FormatStorage(freeBytes)} free  •  {usagePercentage:0.0}% used",

                                Foreground =
                                    (Brush)Resources[
                                        "SecondaryText"],

                                FontSize = 12
                            };

                        content.Children.Add(
                            freeText);

                        card.Child =
                            content;

                        DiskCardsPanel.Children.Add(
                            card);
                    }
                    catch
                    {
                        // Ignore drives that cannot be queried.
                    }
                }

                if (DiskCardsPanel.Children.Count == 0)
                {
                    TextBlock noDrives =
                        new TextBlock
                        {
                            Text =
                                "No available drives found.",

                            Foreground =
                                (Brush)Resources[
                                    "SecondaryText"],

                            FontSize = 14
                        };

                    DiskCardsPanel.Children.Add(
                        noDrives);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Disk analyzer error: {ex.Message}");
            }
        }

        private Dictionary<string, PhysicalDiskInfo>
            GetPhysicalDiskInformation()
        {
            var disks =
                new Dictionary<string, PhysicalDiskInfo>(
                    StringComparer.OrdinalIgnoreCase);

            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT DeviceID, Model, MediaType, InterfaceType, SerialNumber FROM Win32_DiskDrive");

                foreach (ManagementObject disk in searcher.Get())
                {
                    string deviceId =
                        disk["DeviceID"]?.ToString()
                        ?? "";

                    string model =
                        disk["Model"]?.ToString()?.Trim()
                        ?? "Unknown Drive";

                    string mediaType =
                        disk["MediaType"]?.ToString()?.Trim()
                        ?? "";

                    string interfaceType =
                        disk["InterfaceType"]?.ToString()?.Trim()
                        ?? "";

                    string driveType =
                        DetermineDriveType(
                            model,
                            mediaType);

                    string driveInterface =
                        DetermineDriveInterface(
                            model,
                            interfaceType);

                    if (!string.IsNullOrWhiteSpace(
                            deviceId))
                    {
                        disks[deviceId] =
                            new PhysicalDiskInfo
                            {
                                DeviceId =
                                    deviceId,

                                Model =
                                    string.IsNullOrWhiteSpace(
                                        model)
                                        ? "Unknown Drive"
                                        : model,

                                DriveType =
                                    driveType,

                                Interface =
                                    driveInterface
                            };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Physical disk detection error: {ex.Message}");
            }

            return disks;
        }

        private PhysicalDiskInfo
            FindPhysicalDiskForDrive(
                string driveLetter,
                Dictionary<string, PhysicalDiskInfo> physicalDisks)
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT DeviceID, Model, MediaType, InterfaceType, Partitions FROM Win32_DiskDrive");

                foreach (ManagementObject disk in searcher.Get())
                {
                    string model =
                        disk["Model"]?.ToString()?.Trim()
                        ?? "Unknown Drive";

                    string mediaType =
                        disk["MediaType"]?.ToString()?.Trim()
                        ?? "";

                    string interfaceType =
                        disk["InterfaceType"]?.ToString()?.Trim()
                        ?? "";

                    string driveType =
                        DetermineDriveType(
                            model,
                            mediaType);

                    string driveInterface =
                        DetermineDriveInterface(
                            model,
                            interfaceType);

                    string deviceId =
                        disk["DeviceID"]?.ToString()
                        ?? "";

                    try
                    {
                        using ManagementObjectSearcher partitionSearcher =
                            new ManagementObjectSearcher(
                                $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{EscapeWmiPath(deviceId)}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                        foreach (ManagementObject partition in
                                 partitionSearcher.Get())
                        {
                            string partitionDeviceId =
                                partition["DeviceID"]?.ToString()
                                ?? "";

                            if (string.IsNullOrWhiteSpace(
                                    partitionDeviceId))
                            {
                                continue;
                            }

                            using ManagementObjectSearcher logicalSearcher =
                                new ManagementObjectSearcher(
                                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{EscapeWmiPath(partitionDeviceId)}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

                            foreach (ManagementObject logicalDisk in
                                     logicalSearcher.Get())
                            {
                                string logicalDeviceId =
                                    logicalDisk["DeviceID"]?.ToString()
                                    ?? "";

                                if (logicalDeviceId.Equals(
                                        driveLetter,
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    return new PhysicalDiskInfo
                                    {
                                        DeviceId =
                                            deviceId,

                                        Model =
                                            model,

                                        DriveType =
                                            driveType,

                                        Interface =
                                            driveInterface
                                    };
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Continue checking other physical disks.
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Drive mapping error: {ex.Message}");
            }

            return new PhysicalDiskInfo
            {
                DeviceId = "",
                Model = "Physical drive information unavailable",
                DriveType = "Unknown",
                Interface = "Unknown"
            };
        }

        private string EscapeWmiPath(
            string value)
        {
            return value.Replace(
                "'",
                "''");
        }

        private string DetermineDriveType(
            string model,
            string mediaType)
        {
            string combined =
                $"{model} {mediaType}"
                .ToUpperInvariant();

            if (combined.Contains("NVME") ||
                combined.Contains("NVM EXPRESS"))
            {
                return "NVMe SSD";
            }

            if (combined.Contains("SSD") ||
                combined.Contains("SOLID STATE"))
            {
                return "SSD";
            }

            if (combined.Contains("HDD") ||
                combined.Contains("HARD DISK"))
            {
                return "HDD";
            }

            if (mediaType.Contains(
                    "Fixed hard disk",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "SSD / HDD";
            }

            return "Unknown";
        }

        private string DetermineDriveInterface(
            string model,
            string interfaceType)
        {
            string combined =
                $"{model} {interfaceType}"
                .ToUpperInvariant();

            if (combined.Contains("NVME") ||
                combined.Contains("NVM EXPRESS"))
            {
                return "NVMe";
            }

            if (combined.Contains("SATA") ||
                combined.Contains("AHCI"))
            {
                return "SATA";
            }

            if (combined.Contains("USB"))
            {
                return "USB";
            }

            if (combined.Contains("SCSI"))
            {
                return "SCSI";
            }

            if (combined.Contains("IDE"))
            {
                return "IDE";
            }

            if (!string.IsNullOrWhiteSpace(
                    interfaceType))
            {
                return interfaceType;
            }

            return "Unknown Interface";
        }

        private string FormatStorage(
            long bytes)
        {
            double gigabytes =
                bytes /
                1024.0 /
                1024.0 /
                1024.0;

            if (gigabytes >= 1024)
            {
                return
                    $"{gigabytes / 1024:0.0} TB";
            }

            return
                $"{gigabytes:0.0} GB";
        }

        private void RefreshDisksButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoadDisks();
        }

        // ============================================================
        // PROCESS DATA
        // ============================================================

        public class ProcessInfo
        {
            public string Name { get; set; } = "";

            public int Id { get; set; }

            public string CpuUsage { get; set; } = "";

            public double CpuValue { get; set; }

            public string MemoryUsage { get; set; } = "";

            public long MemoryBytes { get; set; }
        }

        private class PhysicalDiskInfo
        {
            public string DeviceId { get; set; } = "";

            public string Model { get; set; } = "Unknown Drive";

            public string DriveType { get; set; } = "Unknown";

            public string Interface { get; set; } = "Unknown";
        }

        protected override void OnClosed(
            EventArgs e)
        {
            _refreshTimer.Stop();

            base.OnClosed(e);
        }
    }
}