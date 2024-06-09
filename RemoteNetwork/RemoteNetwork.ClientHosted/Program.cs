using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteNetwork.Config;
using RemoteNetwork.HostedServices;

namespace RemoteNetwork.ClientHosted
{
    internal class Program
    {
        static void Main(string[] args)
        {
            int workerThreads = 0;
            int completionPortThreads = 0;
            ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);
            ThreadPool.SetMaxThreads(Math.Max(workerThreads, 5000), Math.Max(workerThreads, 5000));
            ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);
            var builder = new HostApplicationBuilder(args);
            builder.Services.AddMemoryCache();
            builder.Services.Configure<TunDriveConfig>(builder.Configuration.GetSection("TunDrive"));
            builder.Services.AddTunDriveHostedService();
            var app = builder.Build();
            app.Run();
        }
    }
}
