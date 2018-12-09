using FluentAssertions;
using Sds.Storage.Blob.WebApi.IntegrationTests;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Sds.Storage.Blob.WebApi.Tests.Tests
{
    public class BlobStorageUsersTestsFixture
    {
        public Guid BlobId { get; set; }

        public BlobStorageUsersTestsFixture(BlobStorageTestHarness harness)
        {
            using (var blobStorage = new BlobStorageClient("osdr_webapi", "osdr_webapi_secret"))
            {
                blobStorage.Authorize("john", "qqq123").Wait();

                BlobId = blobStorage.AddResource(harness.JohnId.ToString(), "Aspirin.mol", new Dictionary<string, object>() { { "parentId", harness.JohnId } }).Result;
            }
        }
    }

    [Collection("BlobStorage Test Harness")]
    public class BlobStorageUsersTests : BlobStorageTest, IClassFixture<BlobStorageUsersTestsFixture>
    {
        private Guid BlobId { get; set; }

        public BlobStorageUsersTests(BlobStorageTestHarness fixture, ITestOutputHelper output, BlobStorageUsersTestsFixture initFixture) : base(fixture, output)
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
            using (var blobStorage = new BlobStorageClient("osdr_webapi", "osdr_webapi_secret"))
            {
                await blobStorage.Authorize("john", "qqq123");

                var info = await blobStorage.GetBlobInfo(JohnId.ToString(), BlobId);

                info["fileName"].Should().Be("Aspirin.mol");
            }
        }
    }
}
