using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using AltinnReStorage.Configuration;
using Azure.Core;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace AltinnReStorage.Services
{
    /// <inheritdoc/>
    public class DocumentClientProvider : IDocumentClientProvider
    {
        private readonly IAccessTokenService _accessTokenService;
        private readonly Dictionary<string, CosmosAccountConfig> _accountConfig;
        private readonly Dictionary<string, DocumentClient> _clients = new Dictionary<string, DocumentClient>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentClientProvider"/> class.
        /// </summary>
        /// <param name="accessTokenService">The access token service.</param>
        /// <param name="cosmosConfig">THe cosmos DB configuration.</param>
        public DocumentClientProvider(IAccessTokenService accessTokenService, IOptions<AzureCosmosConfiguration> cosmosConfig)
        {
            _accessTokenService = accessTokenService;
            _accountConfig = cosmosConfig.Value.AccountConfig;
        }

        /// <inheritdoc/>
        public async Task<DocumentClient> GetDocumentClient(string environment)
        {
            if (_clients.TryGetValue(environment, out DocumentClient documentClient))
            {
                return documentClient;
            }

            try
            {
                _accountConfig.TryGetValue(environment, out CosmosAccountConfig config);

                ConnectionPolicy connectionPolicy = new ConnectionPolicy
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConnectionProtocol = Protocol.Https,
                };

                string token = await _accessTokenService.GetToken(new TokenRequestContext(new[] { "https://management.azure.com/user_impersonation" }));

                using HttpClient client = new HttpClient();

                string url = $"https://management.azure.com/subscriptions/{config.SubscriptionId}/resourceGroups/{config.ResourceGroup}/providers/Microsoft.DocumentDB/databaseAccounts/{config.AccountName}/listKeys?api-version=2020-04-01";
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage res = await client.PostAsync(url, null);

                if (res.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    JObject responseObject = JObject.Parse(await res.Content.ReadAsStringAsync());
                    config.PrimaryKey = responseObject["primaryMasterKey"].ToString();
                    _accountConfig[environment].PrimaryKey = config.PrimaryKey;

                    documentClient = new DocumentClient(new Uri($"https://altinn-{environment}-cosmos-db.documents.azure.com:443/"), config.PrimaryKey, connectionPolicy);
                    _clients.Add(environment, documentClient);
                }

                client.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return documentClient;
        }

        /// <inheritdoc/>
        public void InvalidateDocumentClient(string environment)
        {
            _clients.Remove(environment);
        }

        /// <inheritdoc/>
        public void RemoveDocumentClients()
        {
            foreach (var entry in _accountConfig)
            {
                entry.Value.PrimaryKey = string.Empty;
            }  

            _clients.Clear();
        }
    }
}
