using System;
using System.Collections.Generic;
using System.Text;

namespace AltinnReStorage.Configuration
{
    /// <summary>
    /// Class that contains a dictionary for Azure Cosmos DB account configurations.
    /// </summary>
    public class AzureCosmosConfiguration
    {
        /// <summary>
        /// Account configurations stored by environment.
        /// </summary>
        public Dictionary<string, CosmosAccountConfig> AccountConfig { get; set; }
    }

    /// <summary>
    /// Cosmos DB Account configurations
    /// </summary>
    public class CosmosAccountConfig
    {
        /// <summary>
        /// Account name.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// Resource group.
        /// </summary>
        public string ResourceGroup { get; set; }

        /// <summary>
        /// Subscription id (guid)
        /// </summary>
        public string SubscriptionId { get; set; }

        /// <summary>
        /// PrimaryKey for the database.
        /// </summary>
        public string PrimaryKey { get; set; }
    }
}
