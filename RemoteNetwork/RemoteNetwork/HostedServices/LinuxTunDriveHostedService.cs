using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteNetwork.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RemoteNetwork.HostedServices
{
    public class LinuxTunDriveHostedService : TunDriveHostedService
    {
        public LinuxTunDriveHostedService(IOptions<TunDriveConfig> tunDriveConfigOptions, ILogger<LinuxTunDriveHostedService> logger) : base(tunDriveConfigOptions, logger)
        {
        }
        public override bool ConnectionState(bool connection)
        {
            StartProcess("ip", $"link set {TunDriveConfig.TunDriveName} up");
            Logger.LogInformation($"设置网卡“{TunDriveConfig.TunDriveName}” 为启用状态 mtu：1400");
            StartProcess("ip", $"link set dev {TunDriveConfig.TunDriveName} mtu 1400");
            return true;
        }

        protected override void ConfigIP(string ip, string netmask)
        {
            
            Logger.LogInformation($"设置网卡“{TunDriveConfig.TunDriveName}” IP地址：{ip}/24");
            StartProcess("ip", $"addr add {ip}/24 dev {TunDriveConfig.TunDriveName}");
        }
        [DllImport("libc.so.6", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int Ioctl(SafeHandle device, UInt32 request, byte[] dat);
        private byte[] BytesPlusBytes(byte[] A, byte[] B)
        {
            byte[] ret = new byte[A.Length + B.Length - 1 + 1];
            int k = 0;
            for (var i = 0; i <= A.Length - 1; i++)
                ret[i] = A[i];
            k = A.Length;
            for (var i = k; i <= ret.Length - 1; i++)
                ret[i] = B[i - k];
            return ret;
        }
        protected override FileStream OpenDrive()
        {
            var safeFileHandle = System.IO.File.OpenHandle("/dev/net/tun", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);
            const UInt32 TUNSETIFF = 1074025674;
            byte[] ifreqFREG0 = System.Text.Encoding.ASCII.GetBytes(TunDriveConfig.TunDriveName);
            Array.Resize(ref ifreqFREG0, 16);
            byte[] ifreqFREG1 = { 0x01, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            //IFF_TUN | IFF_NO_PI == 4097 == 1001 == 0x10,0x01  tun0
            byte[] ifreq = BytesPlusBytes(ifreqFREG0, ifreqFREG1);
            int stat = Ioctl(safeFileHandle, TUNSETIFF, ifreq);
            return new FileStream(safeFileHandle, FileAccess.ReadWrite, 1500);
        }
    }
}
