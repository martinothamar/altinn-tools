using System.Collections.Generic;

namespace AltinnReStorage.Configuration
{
    /// <summary>
    /// Class that contains a dictionary for Azure storage account configurations.
    /// </summary>
    public class AzureStorageConfiguration
    {
        /// <summary>
        /// Account configurations stored with the key [org]-[env] e.g. ttd-tt02.
        /// </summary>
        public Dictionary<string, StorageAccountConfig> AccountConfig { get; set; }
    }

    /// <summary>
    /// Storage account configurations
    /// </summary>
    public class StorageAccountConfig
    {
        /// <summary>
        /// Container name
        /// </summary>
        public string Container { get; set; }

        /// <summary>
        /// Storage account name.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// Resource group.
        /// </summary>
        public string ResourceGroup { get; set; }

        /// <summary>
        /// Subscription id (guid).
        /// </summary>
        public string SubscriptionId { get; set; }

        /// <summary>
        /// Primary key for the storage account.
        /// </summary>
        public string AccountKey { get; set; }
    }
}
