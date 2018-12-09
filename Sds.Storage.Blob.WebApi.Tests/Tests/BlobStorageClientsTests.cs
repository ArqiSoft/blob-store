using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Sds.Storage.Blob.WebApi.IntegrationTests
{
    public class BlobStorageClientsTestsFixture
    {
        public Guid BlobId { get; set; }

        public BlobStorageClientsTestsFixture(BlobStorageTestHarness harness)
        {
            using (var blobStorage = new BlobStorageClient("osdr_ml_modeler", "osdr_ml_modeler_secret"))
            {
                blobStorage.AuthorizeClient().Wait();

                BlobId = blobStorage.AddResource("CLIENT_ID", "Aspirin.mol", new Dictionary<string, object>() { { "parentId", harness.JohnId } }).Result;
            }
        }
    }

    [Collection("BlobStorage Test Harness")]
    public class BlobStorageClientsTests : BlobStorageTest, IClassFixture<BlobStorageClientsTestsFixture>
    {
        private Guid BlobId { get; set; }

        public BlobStorageClientsTests(BlobStorageTestHarness fixture, ITestOutputHelper output, BlobStorageClientsTestsFixture initFixture) : base(fixture, output)
        {
            BlobId = initFixture.BlobId;
        }

        [Fact]
        public async Task BlobUpload_ExistingFile_ReturnsGuid()
        {
            BlobId.Should().NotBeEmpty();

            await Task.CompletedTask;
        }

        [Fact]
        public async Task BlobUpload_GetBlobInfo_ReturnsExpectedBlobInfo()
        {
            using (var blobStorage = new BlobStorageClient("osdr_ml_modeler", "osdr_ml_modeler_secret"))
            {
                await blobStorage.AuthorizeClient();

                var info = await blobStorage.GetBlobInfo("CLIENT_ID", BlobId);

                info["fileName"].Should().Be("Aspirin.mol");
            }
        }
    }
}
