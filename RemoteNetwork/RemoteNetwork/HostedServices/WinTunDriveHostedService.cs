using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using RemoteNetwork.Config;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace RemoteNetwork.HostedServices
{
    /// <summary>
    /// LinuxTunDriveHostedService
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WinTunDriveHostedService : TunDriveHostedService
    {
        private readonly static string DriverPath = AppDomain.CurrentDomain.BaseDirectory + "Drivers";
        private const string AdapterKey = "SYSTEM\\CurrentControlSet\\Control\\Class\\{4D36E972-E325-11CE-BFC1-08002BE10318}";
        private const string ConnectionKey = "SYSTEM\\CurrentControlSet\\Control\\Network\\{4D36E972-E325-11CE-BFC1-08002BE10318}";


        public const int TAP_WIN_IOCTL_GET_MAC = 1;
        public const int TAP_WIN_IOCTL_GET_VERSION = 2;
        public const int TAP_WIN_IOCTL_GET_MTU = 3;
        public const int TAP_WIN_IOCTL_GET_INFO = 4;
        public const int TAP_WIN_IOCTL_CONFIG_POINT_TO_POINT = 5;
        public const int TAP_WIN_IOCTL_SET_MEDIA_STATUS = 6;
        public const int TAP_WIN_IOCTL_CONFIG_DHCP_MASQ = 7;
        public const int TAP_WIN_IOCTL_GET_LOG_LINE = 8;
        public const int TAP_WIN_IOCTL_CONFIG_DHCP_SET_OPT = 9;
        public const int TAP_WIN_IOCTL_CONFIG_TUN = 10;

        public const uint FILE_ATTRIBUTE_SYSTEM = 0x4;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        public const uint METHOD_BUFFERED = 0;
        public const uint FILE_ANY_ACCESS = 0;
        public const uint FILE_DEVICE_UNKNOWN = 0x22;
        public WinTunDriveHostedService(IOptions<TunDriveConfig> tunDriveConfigOptions, ILogger<WinTunDriveHostedService> logger) : base(tunDriveConfigOptions, logger)
        {
        }
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool DeviceIoControl(SafeHandle device, uint IoControlCode, IntPtr InBuffer, uint InBufferSize, IntPtr OutBuffer, uint OutBufferSize, ref uint BytesReturned, IntPtr Overlapped);


        protected override FileStream OpenDrive()
        {
            var className = InstallOrGetClassNameDrive();
            var safeFileHandle = System.IO.File.OpenHandle($@"\\.\\Global\\{className}.tap", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);
            return new FileStream(safeFileHandle, FileAccess.ReadWrite, 1500);
        }
        protected virtual string InstallOrGetClassNameDrive()
        {
            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(ConnectionKey))
            {
                var names = registryKey.GetSubKeyNames();
                foreach (var name in names)
                {
                    using (var connectionRegistryKey = registryKey.OpenSubKey(name).OpenSubKey("Connection"))
                    {
                        if (connectionRegistryKey != null && connectionRegistryKey.GetValue("Name").ToString() == TunDriveConfig.TunDriveName)
                        {
                            return name;
                        }
                    }
                }

                Directory.CreateDirectory(DriverPath);
                ZipArchive zipArchive = new ZipArchive(typeof(WinTunDriveHostedService).Assembly.GetManifestResourceStream($"RemoteNetwork.{(Environment.Is64BitOperatingSystem ? "amd64" : "i386")}.zip"), ZipArchiveMode.Read);
                foreach (ZipArchiveEntry entry in zipArchive.Entries)
                {
                    entry.ExtractToFile(Path.Combine(DriverPath, entry.FullName), overwrite: true);
                }
                StartProcess(Path.Combine(DriverPath, "tapinstall.exe"), $"install OemVista.inf TAP0901", "runas", DriverPath);
                foreach (var name in registryKey.GetSubKeyNames())
                {
                    if (!names.Contains(name))
                    {
                        using (var connectionRegistryKey = registryKey.OpenSubKey(name).OpenSubKey("Connection"))
                        {
                            if (connectionRegistryKey != null)
                            {
                                StartProcess("netsh", @$"interface set interface name=""{connectionRegistryKey.GetValue("Name")}"" newname=""{TunDriveConfig.TunDriveName}""");
                                return name;
                            }
                        }
                    }
                }
                return string.Empty;
            }
        }
        private static int ParseIP(string address)
        {
            byte[] addressBytes = address.Split('.').Select(s => byte.Parse(s)).ToArray();
            return addressBytes[0] | (addressBytes[1] << 8) | (addressBytes[2] << 16) | (addressBytes[3] << 24);
        }
        protected override void ConfigIP(string ip, string netmask)
        {
            StartProcess("netsh", $"interface ip set address name=\"{TunDriveConfig.TunDriveName}\" source=static addr={ip} mask={netmask} gateway=none");
            IntPtr intPtr = Marshal.AllocHGlobal(12);
            Marshal.WriteInt32(intPtr, 0, ParseIP(ip));
            Marshal.WriteInt32(intPtr, 4, 0);
            Marshal.WriteInt32(intPtr, 8,0);
            uint lpBytesReturned = 0;
            bool result = DeviceIoControl(TunStream.SafeFileHandle, 2228264, intPtr, 12u, intPtr, 12u, ref lpBytesReturned, IntPtr.Zero);
            Marshal.FreeHGlobal(intPtr);
        }
        private static uint CTL_CODE(uint iDeviceType, uint iFunction, uint iMethod, uint iAccess)
        {
            return ((iDeviceType << 16) | (iAccess << 14) | (iFunction << 2) | iMethod);
        }
        public override bool ConnectionState(bool connection)
        {
            uint Length = 0;
            IntPtr cconfig = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(cconfig, connection ? 1 : 0);

            var b = DeviceIoControl(TunStream.SafeFileHandle, CTL_CODE(FILE_DEVICE_UNKNOWN, TAP_WIN_IOCTL_SET_MEDIA_STATUS, METHOD_BUFFERED, FILE_ANY_ACCESS), cconfig, 4, cconfig, 4, ref Length, IntPtr.Zero);
            StartProcess("netsh", $"netsh interface ipv4 set subinterface \"{TunDriveConfig.TunDriveName}\" mtu=\"1400\" store=persistent");
            return b;
        }
    }
}
