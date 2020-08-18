using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace AltinnReStorage.Services
{
    /// <summary>
    /// Service that handles authorization towards Azure AD.
    /// </summary>
    public interface IAccessTokenService
    {
        /// <summary>
        /// Retrieves an access token for the provided context.
        /// </summary>
        /// <param name="requestContext">Token request context e.g. https://management.azure.com/user_impersonation."</param>
        /// <returns>The access token.</returns>
        public Task<string> GetToken(TokenRequestContext requestContext);

        /// <summary>
        /// Retrieves the interactive browser credential for the current session.
        /// </summary>
        /// <returns>The interactive browser credential.</returns>
        public InteractiveBrowserCredential GetCredential();

        /// <summary>
        /// Invalidates InteractiveBrowserCredential.
        /// </summary>
        public void InvalidateCredentials();
    }
}
