using System;
using System.Threading.Tasks;
using AltinnReStorage.Services;
using Azure.Core;
using McMaster.Extensions.CommandLineUtils;

namespace AltinnReStorage.Commands.Settings
{
    /// <summary>
    /// Logout command handler. Subcommand of Settings.
    /// </summary>
    [Command(
     Name = "Login",
     Description = "Login using Azure AD. All credential will be saved locally.",
     OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
     UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    public class Login : IBaseCmd
    {
        private readonly IAccessTokenService _accessTokenService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Login"/> class.
        /// </summary>
        public Login(IAccessTokenService accessTokenService)
        {
            _accessTokenService = accessTokenService;
        }

        /// <summary>
        /// Logs in the user. User will be propmpted for credentials.
        /// </summary>
        protected override async Task OnExecuteAsync(CommandLineApplication app)
        {
            try
            {
                _accessTokenService.InvalidateCredentials();
                await _accessTokenService.GetToken(new TokenRequestContext(new[] { "https://management.azure.com/user_impersonation" }));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
