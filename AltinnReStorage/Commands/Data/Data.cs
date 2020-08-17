using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace AltinnReStorage.Commands.Data
{
    /// <summary>
    /// Data command handler. Subcommand of Storage.
    /// </summary>
    [Command(
      Name = "data",
      OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
      UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    [Subcommand(typeof(List), typeof(Info), typeof(Undelete), typeof(Restore))]
    public class Data : IBaseCmd
    {
        /// <inheritdoc/>    
        protected override Task OnExecuteAsync(CommandLineApplication app)
        {
            app.ShowHelp();
            return Task.CompletedTask;
        }
    }
}
