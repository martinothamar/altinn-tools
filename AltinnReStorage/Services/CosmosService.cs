using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Altinn.Platform.Storage.Interface.Models;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;

namespace AltinnReStorage.Services
{
    /// <inheritdoc/>
    public class CosmosService : ICosmosService
    {
        private readonly IDocumentClientProvider _clientProvider;
        private readonly Uri _instanceCollectionUri;
        private readonly Uri _dataCollectionUri;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosService"/> class.
        /// </summary>
        /// <param name="clientProvider">The document client provider.</param>
        public CosmosService(IDocumentClientProvider clientProvider)
        {
            _clientProvider = clientProvider;
            _instanceCollectionUri = UriFactory.CreateDocumentCollectionUri("Storage", "instances");
            _dataCollectionUri = UriFactory.CreateDocumentCollectionUri("Storage", "dataElements");
        }

        /// <summary>
        /// Gets metadata for the given data element.
        /// </summary>
        /// <remarks>
        /// Include instanceGuid to avoid a cross partition query.
        /// </remarks>
        public async Task<DataElement> GetDataElement(string dataGuid, string instanceGuid = "")
        {
            DataElement dataElement = null;

            Uri uri = UriFactory.CreateDocumentUri("Storage", "dataElements", dataGuid);
            DocumentClient client = await _clientProvider.GetDocumentClient(Program.Environment);
            FeedOptions options;

            if (!string.IsNullOrEmpty(instanceGuid))
            {
                options = new FeedOptions { PartitionKey = new PartitionKey(instanceGuid) };
            }
            else
            {
                options = new FeedOptions { EnableCrossPartitionQuery = true };
            }

            IDocumentQuery<DataElement> query = client
                .CreateDocumentQuery<DataElement>(_dataCollectionUri, options)
                .Where(i => i.Id == dataGuid)
                .AsDocumentQuery();

            FeedResponse<DataElement> result = await query.ExecuteNextAsync<DataElement>();
            if (result.Count > 0)
            {
                dataElement = result.First();
            }

            return dataElement;
        }

        /// <inheritdoc/>
        public async Task<List<string>> ListDataElements(string instanceGuid)
        {
            List<string> dataGuids = new List<string>();
            string continuationToken = null;

            DocumentClient client = await _clientProvider.GetDocumentClient(Program.Environment);

            do
            {
                var feed = await client.ReadDocumentFeedAsync(
                    _dataCollectionUri,
                    new FeedOptions
                    {
                        PartitionKey = new PartitionKey(instanceGuid),
                        RequestContinuation = continuationToken
                    });

                continuationToken = feed.ResponseContinuation;

                foreach (Document document in feed)
                {
                    dataGuids.Add(document.Id);
                }
            }
            while (continuationToken != null);

            return dataGuids;
        }

        /// <inheritdoc/>
        public async Task<bool> SaveDataElement(DataElement dataElement)
        {
            DocumentClient client = await _clientProvider.GetDocumentClient(Program.Environment);
            ResourceResponse<Document> createDocumentResponse = await client.CreateDocumentAsync(_dataCollectionUri, dataElement);
            HttpStatusCode res = createDocumentResponse.StatusCode;

            return res == HttpStatusCode.Created;
        }

        /// <inheritdoc/>
        public async Task<bool> ReplaceDataElement(DataElement dataElement)
        {
            DocumentClient client = await _clientProvider.GetDocumentClient(Program.Environment);
            ResourceResponse<Document> createDocumentResponse = await client.ReplaceDocumentAsync($"{_dataCollectionUri}/docs/{dataElement.Id}", dataElement);
            HttpStatusCode res = createDocumentResponse.StatusCode;
            return res == HttpStatusCode.OK;
        }
    }
}
