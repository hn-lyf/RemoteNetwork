using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteNetwork.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteNetwork.HostedServices
{
    //TunDriveExtensions
    public abstract class TunDriveHostedService : BackgroundService
    {
        private readonly TunDriveConfig _tunDriveConfig;
        private readonly ILogger _logger;
        public static TunDriveHostedService Instance { get; private set; }
        public TunDriveConfig TunDriveConfig => _tunDriveConfig;

        public FileStream TunStream { get => tunStream; }

        public TunDriveHostedService(IOptions<TunDriveConfig> tunDriveConfigOptions, ILogger logger)
        {
            _tunDriveConfig = tunDriveConfigOptions.Value ?? new TunDriveConfig();
            _logger = logger;
            Instance=this;
            Id = TunDriveConfig.TunDriveIP.Split('.').Select(x => byte.Parse(x)).ToArray();
        }
        private FileStream tunStream;
        /// <summary>
        /// 本机Id
        /// </summary>
        public virtual byte[] Id { get; set; } = new byte[] { 6,6,6,6 };

        public ILogger Logger => _logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            tunStream = OpenDrive();
            Logger.LogInformation($"本机IP：{TunDriveConfig.TunDriveIP}");
            ConfigIP(TunDriveConfig.TunDriveIP, "255.255.255.0");
            ConnectionState(true);
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Yield();
                    var buffer = new Memory<byte>(new byte[10240]);
                    int length = await TunStream.ReadAsync(buffer, stoppingToken).ConfigureAwait(false);
                    _= OnTunReceiveAsync(buffer.Slice(0, length), stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                tunStream.Dispose();
            }
            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Yield();
                await ExecuteAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        protected virtual async ValueTask OnTunReceiveAsync(Memory<byte> buffer, CancellationToken stoppingToken)
        {
            if (buffer.Span[0] != 0x45)//暂时不考虑别的，只考虑IPV4
            {
                return;
            }
            
           await TunNetWorkFrameHostedService.Instance.WriteFrameBufferAsync(buffer, stoppingToken).ConfigureAwait(false);
            //var destId = BitConverter.ToInt32(buffer.Slice(16,4).ToArray(), 0);// string.Join(".", buffer.Slice(16, 4).ToArray());// span[16] << 24 | span[17] << 16 | span[18] << 8 | span[19];
           // var sourceId = BitConverter.ToInt32(buffer.Slice(12, 4).ToArray(), 0);
            
           // return ValueTask.CompletedTask;
        }
        public virtual void WriteFrameBuffer(byte[] buffer)
        {
            try
            {
                TunStream.Write(buffer);
                TunStream.Flush();
            }
            catch (Exception ex)
            {

            }
        }
        
        public virtual string StartProcess(string fileName, string arguments, string verb = null, string workingDirectory = null)
        {
            string empty = string.Empty;
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                    FileName = fileName,
                    Verb = verb
                };
                process.Start();
                process.WaitForExit();
                empty = process.StandardOutput.ReadToEnd();
                process.Close();
            }
            return empty;
        }
        protected abstract void ConfigIP(string ip, string netmask);
        public abstract bool ConnectionState(bool connection);
        protected abstract FileStream OpenDrive();
    }
}
