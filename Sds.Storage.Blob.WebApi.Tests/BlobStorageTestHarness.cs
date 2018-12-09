using MassTransit;
using MassTransit.RabbitMqTransport;
using MassTransit.Testing.MessageObservers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sds.MassTransit.Observers;
using Sds.MassTransit.Settings;
using Sds.Serilog;
using Sds.Storage.Blob.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sds.Storage.Blob.WebApi.IntegrationTests
{
    public class BlobStorageTestHarness : IDisposable
    {
        protected IServiceProvider _serviceProvider;

        public Guid JohnId { get; protected set; }
        public Guid JaneId { get; protected set; }

        public BlobStorageClient JohnBlobStorageClient { get; } = new BlobStorageClient("osdr_webapi", "osdr_webapi_secret");
        public BlobStorageClient JaneBlobStorageClient { get; } = new BlobStorageClient("osdr_webapi", "osdr_webapi_secret");

        public IBusControl BusControl { get { return _serviceProvider.GetService<IBusControl>(); } }

        private IDictionary<Guid, IList<Guid>> ProcessedRecords { get; } = new Dictionary<Guid, IList<Guid>>();
        private IDictionary<Guid, IList<Guid>> InvalidRecords { get; } = new Dictionary<Guid, IList<Guid>>();
        private IDictionary<Guid, int> PersistedRecords { get; } = new Dictionary<Guid, int>();
        private IDictionary<Guid, IList<Guid>> DependentFiles { get; } = new Dictionary<Guid, IList<Guid>>();

        private List<ExceptionInfo> Faults = new List<ExceptionInfo>();

        public ReceivedMessageList Received { get; } = new ReceivedMessageList(TimeSpan.FromSeconds(10));

        public BlobStorageTestHarness()
        {
            var configuration = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", true, true)
                 .AddEnvironmentVariables()
                 .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .MinimumLevel.ControlledBy(new EnvironmentVariableLoggingLevelSwitch("%OSDR_LOG_LEVEL%"))
                .CreateLogger();

            Log.Information("Staring integration tests");

            JaneBlobStorageClient.Authorize("jane", "qqq123").Wait();
            JohnBlobStorageClient.Authorize("john", "qqq123").Wait();

            var services = new ServiceCollection();

            services.AddOptions();
            services.Configure<MassTransitSettings>(configuration.GetSection("MassTransit"));

            services.AddSingleton(container => Bus.Factory.CreateUsingRabbitMq(x =>
            {
                var mtSettings = container.GetService<IOptions<MassTransitSettings>>().Value;

                IRabbitMqHost host = x.Host(new Uri(Environment.ExpandEnvironmentVariables(mtSettings.ConnectionString)), h => { });

                x.ReceiveEndpoint(host, "processing_fault_queue", e =>
                {
                    e.Handler<Fault>(async context =>
                    {
                        Faults.AddRange(context.Message.Exceptions.Where(ex => !ex.ExceptionType.Equals("System.InvalidOperationException")));

                        await Task.CompletedTask;
                    });
                });

                x.ReceiveEndpoint(host, "processing_update_queue", e =>
                {
                    e.Handler<BlobLoaded>(context => { Received.Add(context); return Task.CompletedTask; });
                });

            }));

            _serviceProvider = services.BuildServiceProvider();

            var busControl = _serviceProvider.GetRequiredService<IBusControl>();

            busControl.ConnectPublishObserver(new PublishObserver());
            busControl.ConnectConsumeObserver(new ConsumeObserver());

            busControl.Start();
        }

        public virtual void Dispose()
        {
            var busControl = _serviceProvider.GetRequiredService<IBusControl>();
            busControl.Stop();

            JaneBlobStorageClient.Dispose();
            JohnBlobStorageClient.Dispose();
        }
    }
}
