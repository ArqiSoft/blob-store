using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Sds.Storage.Blob.WebApi.IntegrationTests
{
    public class BlobStorageClient : HttpClient
    {
        public Uri BaseUri { get; }
        public string ClientId { get; }
        public string ClientSecret { get; }

        private KeyCloakClient keycloak;

        public BlobStorageClient(string clientId, string secret)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            BaseUri = new Uri(Environment.ExpandEnvironmentVariables(configuration["BlobStorage:BaseUri"]));

            ClientId = clientId;
            ClientSecret = secret;

            keycloak = new KeyCloakClient(ClientId, ClientSecret);
        }

        public new void Dispose()
        {
            if (keycloak != null)
                keycloak.Dispose();

            base.Dispose();
        }

        public async Task Authorize(string username, string password)
        {
            var token = await keycloak.GetToken(username, password);

            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);
        }

        public async Task AuthorizeClient()
        {
            var token = await keycloak.GetClientToken();

            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);
        }

        public async Task<HttpResponseMessage> UploadFile(string bucket, string filePath, IDictionary<string, object> metadata = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(filePath);

            var requestContent = new MultipartFormDataContent();

            metadata?.Keys.ToList().ForEach(key =>
            {
                if (metadata[key] is string || metadata[key] is Guid)
                {
                    requestContent.Add(new StringContent(metadata[key].ToString()), key);
                }
                else
                {
                    requestContent.Add(new StringContent(JsonConvert.SerializeObject(metadata[key])), key);
                }
            });

            StreamContent streamContent = new StreamContent(File.OpenRead(filePath));
            var fileContent = new ByteArrayContent(await streamContent.ReadAsByteArrayAsync());
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            requestContent.Add(fileContent, "File", Path.GetFileName(filePath));

            return await PostAsync(new Uri(BaseUri, bucket), requestContent);
        }

        public async Task<Guid> Upload(string bucket, string filePath, IDictionary<string, object> metadata = null)
        {
            HttpResponseMessage response = await UploadFile(bucket, filePath, metadata);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception($"Can't upload blob from {filePath}. Status Code: {response.StatusCode}; Reason: {response.ReasonPhrase}; Content: {content}");
            }

            var guids = JsonConvert.DeserializeObject<string[]>(await response.Content.ReadAsStringAsync()).Select(s => Guid.Parse(s));

            return guids.First();
        }

        public async Task<IDictionary<string, object>> GetBlobInfo(string bucket, Guid blobId)
        {
            HttpResponseMessage response = await GetAsync(new Uri(BaseUri, $"{bucket}/{blobId}/info"));

            var json = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        }
    }

    public static class BlobStorageClientExtensions
    {
        public static async Task<Guid> AddResource(this BlobStorageClient client, string bucket, string fileName, IDictionary<string, object> metadata = null)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", fileName);

            return await client.Upload(bucket, filePath, metadata);
        }
    }
}