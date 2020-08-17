using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Storage.Interface.Models;

namespace AltinnReStorage.Services
{
    /// <summary>
    /// The service that handles interaction with Azure Cosmos DB.
    /// </summary>
    public interface ICosmosService
    {
        /// <summary>
        /// List the data guid for all active data elements for the given instance.
        /// </summary>
        public Task<List<string>> ListDataElements(string instanceGuid);

        /// <summary>
        /// Gets metadata for the given data element.
        /// </summary>
        public Task<DataElement> GetDataElement(string dataGuid, string instanceGuid);

        /// <summary>
        /// Stores data element metadata in cosmos.
        /// </summary>
        public Task<bool> SaveDataElement(DataElement dataElement);

        /// <summary>
        /// Replaces and existing document in cosmos.
        /// </summary>
        public Task<bool> ReplaceDataElement(DataElement dataElement);
    }
}
