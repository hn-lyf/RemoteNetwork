using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteNetwork.Config;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteNetwork.HostedServices
{
    /// <summary>
    /// P2P UDP打洞服务
    /// </summary>
    public class P2PUDPSocketHostedService : BackgroundService
    {
        private readonly string P2PHost = "";
        private const int P2PPort = 61001;

        private readonly ILogger<P2PUDPSocketHostedService> _logger;
        private readonly ConcurrentDictionary<long, P2PUDPSocket> p2pSocket = new ConcurrentDictionary<long, P2PUDPSocket>();
        private readonly IMemoryCache memoryCache;
        private readonly ConcurrentQueue<int> queue;
        public IMemoryCache MemoryCache => memoryCache;
        public static P2PUDPSocketHostedService Instance { get; private set; }

        public ConcurrentDictionary<long, P2PUDPSocket> P2pSocket => p2pSocket;

        public ILogger<P2PUDPSocketHostedService> Logger => _logger;

        public P2PUDPSocketHostedService(ILogger<P2PUDPSocketHostedService> logger, IMemoryCache memoryCache, IOptions<TunDriveConfig> tunDriveConfigOptions)
        {
            P2PHost = tunDriveConfigOptions.Value.P2PHostName;
            _logger = logger;
            this.memoryCache = memoryCache;
            Instance = this;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
           await Task.Delay(-1,stoppingToken).ConfigureAwait(false);
        }
        void ReceiveCallback(IAsyncResult ar)
        {
            var udpClient = ar.AsyncState as UdpClient;
            System.Net.IPEndPoint remoteEndPoint = new System.Net.IPEndPoint(0, 0);
            byte[] bytes = null;
            try
            {

                bytes = udpClient.EndReceive(ar, ref remoteEndPoint);

            }
            finally
            {
                udpClient.BeginReceive(ReceiveCallback, udpClient);
            }
            if (bytes.Length > 8)
            {
                TunDriveHostedService.Instance.WriteFrameBuffer(bytes);
            }
        }
        public virtual async Task TestP2P(byte[] destId,bool sendRequest=true)
        {
            try
            {
                long p2pId = 0;
                if (sendRequest)
                {
                    _logger.LogInformation($"请求打洞到{string.Join('.', destId)},发送打洞请求");
                    await TunNetWorkFrameHostedService.Instance.SendP2PRequestAsync(destId, default).ConfigureAwait(false);
                }
                _logger.LogInformation($"收到请求打洞到{string.Join('.', destId)}");
                UdpClient client = new UdpClient(new System.Net.IPEndPoint(IPAddress.Any, 0));
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    uint IOC_IN = 0x80000000;
                    uint IOC_VENDOR = 0x18000000;
                    uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                    client.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
                }
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                using (System.IO.MemoryStream memoryStream = new MemoryStream())
                {
                    if (sendRequest)
                    {
                        memoryStream.Write(TunDriveHostedService.Instance.Id);
                        memoryStream.Write(destId);
                    }
                    else
                    {
                        memoryStream.Write(destId);
                        memoryStream.Write(TunDriveHostedService.Instance.Id);
                    }
                    p2pId = BitConverter.ToInt64(memoryStream.ToArray());
                    await client.SendAsync(memoryStream.ToArray(), 8, new System.Net.IPEndPoint(Dns.GetHostAddresses(P2PHost)[0], P2PPort));//发送此次打洞的id
                }
                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(3000))
                {
                    var receiveResult = await client.ReceiveAsync(cancellationTokenSource.Token);
                    var text = Encoding.UTF8.GetString(receiveResult.Buffer);
                    var remoteEndPoint = IPEndPoint.Parse(text);//远程端口
                    Logger.LogInformation($"对方【{string.Join('.', destId)}】 的IP：{remoteEndPoint}");

                    await client.SendAsync(new byte[] { 1 }, 1, remoteEndPoint);
                    using (CancellationTokenSource cancellationTokenSource1 = new CancellationTokenSource(3000))
                    {
                        receiveResult = await client.ReceiveAsync(cancellationTokenSource1.Token);
                        await client.SendAsync(new byte[] { 2 }, 1, receiveResult.RemoteEndPoint);

                        if (P2pSocket.TryAdd(p2pId, new P2PUDPSocket(destId, p2pId, client, receiveResult.RemoteEndPoint)))
                        {
                            Logger.LogInformation($"与【{string.Join('.', destId)}】打洞成功 通道{p2pId}  {remoteEndPoint} ");
                        }
                        else
                        {
                            Logger.LogWarning($"与【{string.Join('.', destId)}】打洞通道{p2pId}存在 ");
                        }
                        return;
                    }
                }
            }
            catch
            {

            }
            _logger.LogInformation($"请求打洞到{string.Join('.', destId)} 失败");
        }
        public virtual ITunNetWorkFrameSend GetP2PClient(byte[] dest)
        {
            long p2pId = 0;
            using (System.IO.MemoryStream memoryStream = new MemoryStream())
            {
                memoryStream.Write(TunDriveHostedService.Instance.Id);
                memoryStream.Write(dest);
                p2pId = BitConverter.ToInt64(memoryStream.ToArray());
                if (P2pSocket.TryGetValue(p2pId, out var client))
                {
                    return client;
                }
                memoryStream.Position = 0;
                memoryStream.Write(dest);
                memoryStream.Write(TunDriveHostedService.Instance.Id);
                p2pId = BitConverter.ToInt64(memoryStream.ToArray());
                if (P2pSocket.TryGetValue(p2pId, out  client))
                {
                    return client;
                }
            }
            MemoryCache.GetOrCreate(BitConverter.ToInt32(dest), (ic) =>
            {
                ic.SetSlidingExpiration(TimeSpan.FromMinutes(3));
                System.Threading.ThreadPool.QueueUserWorkItem((s) => {
                    TestP2P(s as byte[]).ConfigureAwait(false);
                },dest);
                return true;
            });
            return null;
        }
    }
}
