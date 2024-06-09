using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteNetwork.ListenerHosted.HostedServices;

namespace RemoteNetwork.ListenerHosted
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var builder = new HostApplicationBuilder(args);
            builder.Services.AddHostedService<ListenerHostedService>();
            builder.Services.AddHostedService<P2PListenerHostedService>();
            builder.Services.AddWindowsService();
            var app = builder.Build();
            app.Run();
        }
    }
}
