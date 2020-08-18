using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace AltinnReStorage.Commands.Settings
{
    /// <summary>
    /// Settings root command.
    /// </summary>
    [Command(
       Name = "settings",
       OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
       UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    [Subcommand(typeof(Login), typeof(Logout), typeof(Update))]
    public class Settings : IBaseCmd
    {
        /// <inheritdoc/>    
        protected override Task OnExecuteAsync(CommandLineApplication app)
        {
            app.ShowHelp();
            return Task.CompletedTask;
        }
    }
}
