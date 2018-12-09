using MassTransit;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Sds.Storage.Blob.WebApi.IntegrationTests
{
    [CollectionDefinition("BlobStorage Test Harness")]
    public class BlobStorageTestCollection : ICollectionFixture<BlobStorageTestHarness>
    {
    }

    public abstract class BlobStorageTest
    {
        public BlobStorageTestHarness Harness { get; }

        protected Guid JohnId => Harness.JohnId;
        protected Guid JaneId => Harness.JaneId;
        protected IBus Bus => Harness.BusControl;

        public BlobStorageTest(BlobStorageTestHarness fixture, ITestOutputHelper output = null)
        {
            Harness = fixture;

            if (output != null)
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo
                    .TestOutput(output, LogEventLevel.Verbose)
                    .CreateLogger()
                    .ForContext<BlobStorageTest>();
            }
        }
    }
}
