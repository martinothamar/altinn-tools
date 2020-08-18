using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace AltinnReStorage.Commands.Instance
{
    /// <summary>
    /// Instance command handler. Subcommand of Storage.
    /// </summary>
    [Command(
      Name = "instance",
      OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
      UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    [Subcommand(typeof(List), typeof(Info))]
    public class Instance : IBaseCmd
    {
        /// <inheritdoc/>    
        protected override Task OnExecuteAsync(CommandLineApplication app)
        {
            app.ShowHelp();
            return Task.CompletedTask;
        }
    }
}
