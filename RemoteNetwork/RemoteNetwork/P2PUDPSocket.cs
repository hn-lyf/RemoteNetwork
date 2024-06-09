using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using RemoteNetwork.HostedServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace RemoteNetwork
{
    public interface ITunNetWorkFrameSend
    {
        Task SendAsync(Memory<byte> buffer, CancellationToken stoppingToken);
    }
    public class P2PUDPSocket : IDisposable, ITunNetWorkFrameSend
    {
        private readonly UdpClient client;
        private readonly IPEndPoint _remoteEndPoint;
        public virtual IPEndPoint RemoteEndPoint => _remoteEndPoint;
        private readonly byte[] _destId;
        private readonly System.Threading.Timer timer;
        private DateTime lastDateTime;
        private DateTime lastReceiveDateTime;

        private bool isRun = true;
        private long p2pId;
       public P2PUDPSocket(byte[] destId,long p2pId, UdpClient client, IPEndPoint remoteEndPoint)
        {
            lastDateTime = DateTime.Now;
            _destId =destId;
            this.client = client;
            _remoteEndPoint = remoteEndPoint;
           this.p2pId=p2pId;
            timer = new System.Threading.Timer(TimerCallback, this, 1000*3, 1000 * 3);
            client.BeginReceive(ReceiveCallback, client);
            lastReceiveDateTime = DateTime.Now;
        }
       
        protected virtual void TimerCallback(object state)
        {
           
            if((DateTime.Now- lastDateTime).Seconds > 10)
            {
                P2PUDPSocketHostedService.Instance.P2pSocket.TryRemove(p2pId, out _);
                P2PUDPSocketHostedService.Instance.MemoryCache.Remove(BitConverter.ToInt32(_destId));
                this.Dispose();
                P2PUDPSocketHostedService.Instance.Logger.LogWarning($"与【{string.Join('.', _destId)}】 通道{p2pId} {RemoteEndPoint} 已废弃");
                return;
            }
            if ((DateTime.Now - lastReceiveDateTime).TotalSeconds > 100)
            {
                _ = SendAsync(new byte[] { 0 }, default).ConfigureAwait(false);
                P2PUDPSocketHostedService.Instance.MemoryCache.Remove(BitConverter.ToInt32(_destId));
                P2PUDPSocketHostedService.Instance.P2pSocket.TryRemove(p2pId, out _);
                this.Dispose();
                P2PUDPSocketHostedService.Instance.Logger.LogWarning($"与【{string.Join('.', _destId)}】通道{p2pId} 长期无消息 通道 {RemoteEndPoint} 已废弃");
                return;
            }
            _ = SendAsync(new byte[] { 2 }, default).ConfigureAwait(false);
        }
        public virtual async Task SendAsync(Memory<byte> buffer, CancellationToken stoppingToken)
        {
            await client.SendAsync(buffer, RemoteEndPoint, stoppingToken).ConfigureAwait(false);
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
            catch(Exception)
            {

            }
            finally
            {
                if (isRun)
                {
                    udpClient.BeginReceive(ReceiveCallback, udpClient);
                }
                
            }
            if (isRun)
            {
                lastDateTime = DateTime.Now;
                timer.Change(1000 * 3, 1000 * 3);
                if (bytes.Length == 1 )
                {
                    if( bytes[0] == 2)
                    {
                        _ = SendAsync(new byte[] { 1 }, default).ConfigureAwait(false);

                    }
                    else if (bytes[0] == 0)
                    {
                        P2PUDPSocketHostedService.Instance.P2pSocket.TryRemove(p2pId, out _);
                        P2PUDPSocketHostedService.Instance.MemoryCache.Remove(BitConverter.ToInt32(_destId));
                        
                        P2PUDPSocketHostedService.Instance.Logger.LogWarning($"与【{string.Join('.', _destId)}】 通道{p2pId} {RemoteEndPoint}  对方发来关闭 已废弃");
                        this.Dispose();
                        return;
                    }
                    return;
                }
                if (bytes.Length > 8)
                {
                    lastReceiveDateTime = DateTime.Now;
                    TunDriveHostedService.Instance.WriteFrameBuffer(bytes);
                }
            }
        }
        public void Dispose()
        {
            isRun = false;
            timer.Dispose();
            client.Dispose();
        }
    }
}
