using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;

namespace AltinnReStorage.Services
{
    /// <summary>
    /// Service that provides and administers Azure CosmosDB document clients.
    /// </summary>
    public interface IDocumentClientProvider
    {
        /// <summary>
        /// Retrieves a document client for the given context.
        /// </summary>
        /// <param name="environment">The environment the database exists in.</param>
        /// <returns>The document client.</returns>
        Task<DocumentClient> GetDocumentClient(string environment);

        /// <summary>
        /// Invalidates the document client if it exists.
        /// </summary>
        /// <param name="environment">The environment.</param>
        void InvalidateDocumentClient(string environment);

        /// <summary>
        /// Deletes all cached document clients and the database primary keys.
        /// </summary>
        void RemoveDocumentClients();
    }
}
