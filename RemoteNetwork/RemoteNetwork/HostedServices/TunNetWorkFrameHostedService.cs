using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteNetwork.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteNetwork.HostedServices
{
    public class TunNetWorkFrameHostedService : BackgroundService
    {
        private  readonly string exchangeHostName = "";
        private readonly int P2PPort = 61000;
        protected readonly ILogger<TunNetWorkFrameHostedService> _logger;
        public static TunNetWorkFrameHostedService Instance { get; private set; }
        private readonly UdpClient udpClient;
        private readonly System.Net.IPEndPoint remoteEndPoint = new System.Net.IPEndPoint(0, 0);
        public TunNetWorkFrameHostedService(ILogger<TunNetWorkFrameHostedService> logger, IOptions<TunDriveConfig> tunDriveConfigOptions)
        {
            exchangeHostName = tunDriveConfigOptions.Value.DataExchangeHostName;
            _logger = logger;
            Instance = this;
            udpClient = new UdpClient(0); if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                const int SIP_UDP_CONNRESET = -1744830452;
                udpClient.Client.IOControl(SIP_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            udpClient.BeginReceive(ReceiveCallback, udpClient);
            while (!stoppingToken.IsCancellationRequested)
            {
                await udpClient.SendAsync(TunDriveHostedService.Instance.Id, exchangeHostName, P2PPort, stoppingToken).ConfigureAwait(false);
                await Task.Delay(1000*30, stoppingToken).ConfigureAwait(false);
            }
        }
        void ReceiveCallback(IAsyncResult ar)
        {
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
            if (bytes.Length == 4)
            {
                return;
            }
            if (bytes.Length == 5)
            {
                if (bytes[0] == 2)
                {
                    P2PUDPSocketHostedService.Instance.TestP2P(bytes.Skip(1).ToArray(),false);
                }
                return;
            }
           
            TunDriveHostedService.Instance.WriteFrameBuffer(bytes);
        }
        public virtual async Task WriteFrameBufferAsync(Memory<byte> buffer, CancellationToken stoppingToken)
        { 
            var destId = BitConverter.ToInt32(buffer.Slice(16, 4).ToArray(), 0);

           var tunNetWorkFrameSend= P2PUDPSocketHostedService.Instance.GetP2PClient(buffer.Slice(16, 4).ToArray());
            if (tunNetWorkFrameSend != null)
            {
                await tunNetWorkFrameSend.SendAsync(buffer, stoppingToken).ConfigureAwait(false);
                return;
            }
            var bytes = new byte[buffer.Length + 8];
            buffer.Slice(12, 8).CopyTo(bytes);
            Array.Copy(buffer.ToArray(), 0,bytes,8,buffer.Length);
            await udpClient.SendAsync(bytes, exchangeHostName, P2PPort, stoppingToken).ConfigureAwait(false);
            //var destId = BitConverter.ToInt32(buffer.Slice(16, 4).ToArray(), 0);// string.Join(".", buffer.Slice(16, 4).ToArray());// span[16] << 24 | span[17] << 16 | span[18] << 8 | span[19];
            //var sourceId = BitConverter.ToInt32(buffer.Slice(12, 4).ToArray(), 0);
            //_logger.LogInformation($"{sourceId} 发送到{destId}");
        }
        /// <summary>
        /// 发送打洞请求
        /// </summary>
        /// <param name="destId"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public virtual async Task SendP2PRequestAsync(byte[] destId, CancellationToken stoppingToken)
        {
            using (MemoryStream memoryStream = new MemoryStream()) {
                memoryStream.Write(TunDriveHostedService.Instance.Id);
                memoryStream.Write(destId);
                memoryStream.WriteByte(2);
                memoryStream.Write(TunDriveHostedService.Instance.Id);
                await udpClient.SendAsync(memoryStream.ToArray(), exchangeHostName, P2PPort, stoppingToken).ConfigureAwait(false);
            }
            
        }
    }
}
