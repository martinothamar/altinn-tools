using System;
using System.Threading.Tasks;
using AltinnReStorage.Services;
using McMaster.Extensions.CommandLineUtils;

namespace AltinnReStorage.Commands.Settings
{
    /// <summary>
    /// Update command handler. Subcommand of Settings.
    /// </summary>
    [Command(
         Name = "Update",
         Description = "update application configurations",
         OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
         UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    public class Update : IBaseCmd
    {
        private readonly IAccessTokenService _accessTokenService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Update"/> class.
        /// </summary>
        public Update(IAccessTokenService accessTokenService)
        {
            _accessTokenService = accessTokenService;
        }

        /// <summary>
        /// Platform environment to work with.
        /// </summary>
        [Option(CommandOptionType.SingleValue, ShortName = "e", LongName = "enviroment", Description = "Sets the current context to the given environment ", ValueName = "Platform environment", ShowInHelpText = true)]
        [AllowedValues("at22", "at23", "at24", "tt02", "prod", IgnoreCase = true)]
        public string Environment { get; set; }

        /// <summary>
        /// Updates the app configuration with the provided options.
        /// </summary>
        protected override Task OnExecuteAsync(CommandLineApplication app)
        {
            if (!string.IsNullOrEmpty(Environment))
            {
                // If mocing to of from production, new credentials will be requiered.
                if (!string.IsNullOrEmpty(Program.Environment) && (Program.Environment.Equals("prod") || Environment.Equals("prod")))
                {
                    _accessTokenService.InvalidateCredentials();
                }

                Program.Environment = Environment;

                Console.WriteLine($"Enviroment updated \n");
            }

            return Task.CompletedTask;
        }
    }
}
