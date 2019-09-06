using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Sds.Storage.Blob.WebApi.Settings;
using System.IO;

namespace Sds.Storage.Blob.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            var host = new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseConfiguration(configuration)
                .UseIISIntegration()
                .UseKestrel(cfg => {
                    var settings = cfg.ApplicationServices.GetService(typeof(IOptions<BlobStorageSettings>)) as IOptions<BlobStorageSettings>;
                    if (settings != null)
                    {
                        cfg.Limits.MaxRequestBodySize = settings.Value?.MaxRequestSize;
                    }
                })
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}