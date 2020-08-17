using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace AltinnReStorage.Commands.Instance
{
    /// <summary>
    /// List command handler. Lists instances based on given parameters.
    /// </summary>
    [Command(
      Name = "list",
      OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
      UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    public class List:IBaseCmd
    {
        /// <inheritdoc/>    
        protected override Task OnExecuteAsync(CommandLineApplication app)
        {
            app.ShowHelp();
            return Task.CompletedTask;
        }
    }
}
