using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteNetwork.ListenerHosted.HostedServices
{
    public class ListenerHostedService : BackgroundService
    {
        private readonly ILogger<ListenerHostedService> _logger;
        private readonly UdpClient udpClient;
        private readonly ConcurrentDictionary<int, IPEndPoint> remoteEndPoints = new ConcurrentDictionary<int, IPEndPoint>();
        public ListenerHostedService(ILogger<ListenerHostedService> logger)
        {
            _logger = logger;
            udpClient = new UdpClient(61000);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                const int SIP_UDP_CONNRESET = -1744830452;
                udpClient.Client.IOControl(SIP_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
        }
        private bool IsRun = false;

        public ILogger<ListenerHostedService> Logger => _logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            IsRun = true;
            Logger.LogInformation($"远程组网服务端中转服务已开启，端口{61000}");

            udpClient.BeginReceive(ReceiveCallback, udpClient);
            await Task.Delay(-1, stoppingToken).ConfigureAwait(false);
            IsRun = false;
        }
        protected virtual void ReceiveCallback(IAsyncResult ar)
        {
            System.Net.IPEndPoint remoteEndPoint = new System.Net.IPEndPoint(0, 0);
            byte[] bytes = null;
            try
            {

                bytes = udpClient.EndReceive(ar, ref remoteEndPoint);

            }
            finally
            {
                if (IsRun)
                {
                    udpClient.BeginReceive(ReceiveCallback, udpClient);
                }
            }
            var sourceId = BitConverter.ToInt32(bytes, 0);
            remoteEndPoints.AddOrUpdate(sourceId, remoteEndPoint, (k, o) => {

                Logger.LogWarning($"{string.Join('.', bytes.Take(4))} 从原来的{o} 变化为{remoteEndPoint}");
                return remoteEndPoint;
            });
            if (bytes.Length == 4)
            {
                udpClient.Send(bytes, remoteEndPoint);
                return;
            }
            var destId = BitConverter.ToInt32(bytes, 4);
            if (bytes.Length == 8)
            {
                Logger.LogInformation($"{string.Join('.', bytes.Take(4))} 准备向 {string.Join('.', bytes.Skip(4).Take(4))} 打洞 {remoteEndPoint}");
                return;
            }
            if (remoteEndPoints.TryGetValue(destId, out var remotePoint))
            {
                udpClient.Send(bytes.Skip(8).ToArray(), remotePoint);//发送到客户
            }
            else
            {
                Logger.LogWarning($"【{remoteEndPoint}】{string.Join('.', bytes.Take(4))} 准备向 {string.Join('.', bytes.Skip(4).Take(4))} 发送的数据无客户端，抛弃");
            }
        }
    }
}
