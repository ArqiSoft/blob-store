using FluentAssertions;
using Sds.Storage.Blob.WebApi.IntegrationTests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Sds.Storage.Blob.WebApi.Tests.Tests
{
    [Collection("BlobStorage Test Harness")]
    public class BlobStorageTooBigFileTests : BlobStorageTest
    {
        private Guid BlobId { get; set; }

        public BlobStorageTooBigFileTests(BlobStorageTestHarness fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }

        [Fact]
        public async Task BlobUpload_UploadTooBitFile_Returns413()
        {
            using (var blobStorage = new BlobStorageClient("osdr_webapi", "osdr_webapi_secret"))
            {
                blobStorage.Authorize("john", "qqq123").Wait();

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "TooBig.mol");

                var response = await blobStorage.UploadFile(Harness.JohnId.ToString(), filePath, new Dictionary<string, object>() { { "parentId", Harness.JohnId } });

                response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
            }
        }
    }
}
