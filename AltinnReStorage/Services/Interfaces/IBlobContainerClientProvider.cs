using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace AltinnReStorage.Services
{
    /// <summary>
    /// Service that provides and administers Azure BlobContainerClients.
    /// </summary>
    public interface IBlobContainerClientProvider
    {
        /// <summary>
        /// Retrieves a blob client for the given context.
        /// </summary>
        /// <param name="org">The organisation that owns the blob container.</param>
        /// <param name="environment">The environment the container exists in.</param>
        /// <returns>The blob container client.</returns>
        Task<BlobContainerClient> GetBlobClient(string org, string environment);

        /// <summary>
        /// Invalidates the blob container client if it exists.
        /// </summary>
        /// <param name="org">The organisation that owns the blob container.</param>
        /// <param name="environment">The environment.</param>
        void InvalidateBlobClient(string org, string environment);

        /// <summary>
        /// Deletes all cached blob clients and their storage account keys.
        /// </summary>
        void RemoveBlobClients();
    }
}
