using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteNetwork.HostedServices;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteNetwork
{
    public static class TunDriveExtensions
    {
        public static IServiceCollection AddTunDriveHostedService(this IServiceCollection services)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                services.AddHostedService<WinTunDriveHostedService>();
            }
            else
            {
                services.AddHostedService<LinuxTunDriveHostedService>();
            }
            services.AddHostedService<TunNetWorkFrameHostedService>();
            services.AddHostedService<P2PUDPSocketHostedService>();
            return services;
        }
    }
}
