using System;
using System.Collections.Generic;
using System.Text;

namespace AltinnReStorage.Configuration
{
    /// <summary>
    /// General settings for the application
    /// </summary>
    public class GeneralSettings
    {
        /// <summary>
        /// The cliend id registered in Azure
        /// </summary>
        public string ClientId { get; set; }
        
        /// <summary>
        /// The tenant id registered in Azure
        /// </summary>
        public string TenantId { get; set; }
    }
}
