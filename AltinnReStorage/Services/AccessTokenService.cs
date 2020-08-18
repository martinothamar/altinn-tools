using System.Threading.Tasks;
using AltinnReStorage.Configuration;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace AltinnReStorage.Services
{
    /// <inheritdoc/>
    public class AccessTokenService : IAccessTokenService
    {
        private InteractiveBrowserCredential _credential;
        private readonly GeneralSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessTokenService"/> class.
        /// </summary>
        /// <param name="settings">The general settings.</param>
        public AccessTokenService(IOptions<GeneralSettings> settings)
        {
            _settings = settings.Value;
        }

        /// <inheritdoc/>
        public async Task<string> GetToken(TokenRequestContext requestContext)
        {
            AccessToken token = await GetCredential().GetTokenAsync(requestContext);

            return token.Token;
        }

        /// <inheritdoc/>
        public InteractiveBrowserCredential GetCredential()
        {
            if (_credential == null)
            {
                _credential = new InteractiveBrowserCredential(_settings.TenantId, _settings.ClientId);
            }

            return _credential;
        }

        /// <inheritdoc/>
        public void InvalidateCredentials()
        {
            _credential = null;
        }
    }
}