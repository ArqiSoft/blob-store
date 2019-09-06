using Flurl;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Sds.Storage.Blob.WebApi.IntegrationTests
{
    public class Token
    {
        public string access_token { get; set; }
    }

    public class UserInfo
    {
        public Guid sub { get; set; }
        public string email { get; set; }
        public string name { get; set; }
        public string preferred_username { get; set; }
        public string given_name { get; set; }
        public string family_name { get; set; }
    }

    public class KeyCloakClient : HttpClient
    {
        public string Authority { get; }
        public string ClientId { get; private set; }
        public string ClientSecret { get; private set; }

        public KeyCloakClient(string clientId, string secret) : base()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            Authority = Environment.ExpandEnvironmentVariables(configuration["KeyCloak:Authority"]);

            SetClient(clientId, secret);
        }

        public void SetClient(string clientId, string secret)
        {
            ClientId = clientId;
            ClientSecret = secret;

            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{secret}")));
        }

        public async Task<Token> GetToken(string username, string password)
        {
            var nvc = new List<KeyValuePair<string, string>>();
            nvc.Add(new KeyValuePair<string, string>("username", username));
            nvc.Add(new KeyValuePair<string, string>("password", password));
            nvc.Add(new KeyValuePair<string, string>("grant_type", "password"));

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Url.Combine(Authority, "protocol/openid-connect/token"))) { Content = new FormUrlEncodedContent(nvc) };

            var response = await SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<Token>(json);
        }

        public async Task<Token> GetClientToken()
        {
            var nvc = new List<System.Collections.Generic.KeyValuePair<string, string>>();
            nvc.Add(new KeyValuePair<string, string>("grant_type", "client_credentials"));

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Url.Combine(Authority, "protocol/openid-connect/token"))) { Content = new FormUrlEncodedContent(nvc) };

            var json = await SendAsync(request).Result.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<Token>(json);
        }

        public async Task<UserInfo> GetUserInfo(string username, string password)
        {
            var token = await GetToken(username, password);

            return await GetUserInfo(token.access_token);
        }

        public async Task<UserInfo> GetUserInfo(string token)
        {
            DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var userInfoResponse = await GetAsync(new Uri(Url.Combine(Authority, "protocol/openid-connect/token")));

            var json = await userInfoResponse.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<UserInfo>(json);
        }
    }
}