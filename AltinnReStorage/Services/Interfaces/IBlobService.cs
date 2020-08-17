using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Platform.Storage.Interface.Models;
using AltinnReStorage.Enums;
using Azure.Storage.Blobs.Models;

namespace AltinnReStorage.Services
{
    /// <summary>
    /// The service that handles interaction with Azure Blob Storage.
    /// </summary>
    public interface IBlobService
    {      
        /// <summary>
        /// Lists blobs related to the instance in the given state(s).
        /// </summary>
        public Task<List<BlobItem>> ListBlobs(string org, string app, string instanceGuid, ElementState state);

        /// <summary>
        /// Lists all available snapshots (versions) of the blob.
        /// </summary>
        public Task<List<string>> ListBlobVersions(string org, string app, string instanceGuid, string dataGuid);

        /// <summary>
        /// Undeletes a blob by restoring the most recent snapshot.
        /// </summary>
        public Task<bool> UndeleteBlob(string org, string app, string instanceGuid, string dataGuid);

        /// <summary>
        /// Restored a blob from a snapshot corresponding to the given snapshot.
        /// </summary>
        public Task<bool> RestoreBlob(string org, string app, string instanceGuid, string dataGuid, string restoreTimestamp);

        /// <summary>
        /// Gets the metadata backup for a data element.
        /// </summary>
        public Task<DataElement> GetDataElementBackup(string instanceGuid, string dataGuid);

        /// <summary>
        /// Gets the metadata backup for a data element based on the restore timestamp.
        /// </summary>
        public Task<DataElement> GetDataElementBackup(string instanceGuid, string dataGuid, string restoreTimestamp = "");
    }
}
