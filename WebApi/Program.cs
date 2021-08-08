using DTO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace WebApi
{
    public class Program
    {
        internal static Func<ICompounder[]> GetInstances { get; private set; }

        public static async void Start(string webApiUrl, Func<ICompounder[]> getInstances, string[] args)
        {
            GetInstances = getInstances;

            IHost host = Host
                .CreateDefaultBuilder(args)
                .ConfigureLogging(log => log.ClearProviders())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseStartup<Startup>()
                        .UseUrls(webApiUrl);
                })
                .Build();

            await Task.Factory.StartNew(() => host.Run(), TaskCreationOptions.LongRunning);
        }
    }
}
