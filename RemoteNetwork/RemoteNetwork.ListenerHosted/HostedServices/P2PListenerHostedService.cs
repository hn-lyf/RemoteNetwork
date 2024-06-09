using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace RemoteNetwork.ListenerHosted.HostedServices
{
    public class P2PListenerHostedService : BackgroundService
    {
        private readonly ILogger<P2PListenerHostedService> _logger;
        private readonly UdpClient udpClient;
        private readonly ConcurrentDictionary<long, IPEndPoint> p2pUdpEndPoints = new ConcurrentDictionary<long, IPEndPoint>();
        private readonly TcpListener tcpListener;
        public P2PListenerHostedService(ILogger<P2PListenerHostedService> logger)
        {
            _logger = logger;
            udpClient = new UdpClient(61001);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                const int SIP_UDP_CONNRESET = -1744830452;
                udpClient.Client.IOControl(SIP_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
        }
        private bool IsRun = false;

        public ILogger<P2PListenerHostedService> Logger => _logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            IsRun = true;
            Logger.LogInformation($"打洞服务器 UDP开启，端口{61001}");
            udpClient.BeginReceive(ReceiveCallback, udpClient);
            await Task.Delay(-1, stoppingToken).ConfigureAwait(false);
            IsRun = false;
        }
        protected virtual async void ReceiveCallback(IAsyncResult ar)
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
            if (bytes.Length != 8)
            {
                return;
            }
            var sourceId = BitConverter.ToInt32(bytes, 0);
            var destId = BitConverter.ToInt32(bytes, 4);
            _logger.LogWarning($"{string.Join('.', bytes.Take(4))} 打洞到{string.Join('.', bytes.Skip(4).Take(4))} 链接{remoteEndPoint}");
            var id= BitConverter.ToInt64(bytes, 0);
            if (p2pUdpEndPoints.TryAdd(id, remoteEndPoint))
            {
                await Task.Delay(3000).ConfigureAwait(false);
                if (p2pUdpEndPoints.ContainsKey(id))//间隔5s还没被接受，就认为是死链
                {
                    p2pUdpEndPoints.TryRemove(id, out _);
                    _logger.LogWarning($"{remoteEndPoint} udp 无接受端 被关闭");
                }
                return;
            }
            if (p2pUdpEndPoints.TryRemove(id,out var endPoint))
            {
                udpClient.Send(System.Text.Encoding.UTF8.GetBytes(endPoint.ToString()), remoteEndPoint);
                udpClient.Send(System.Text.Encoding.UTF8.GetBytes(remoteEndPoint.ToString()), endPoint);
                return;
            }


        }
    }
}
