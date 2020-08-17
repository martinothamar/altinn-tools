using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Altinn.Platform.Storage.Interface.Models;
using AltinnReStorage.Attributes;
using AltinnReStorage.Services;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace AltinnReStorage.Commands.Data
{
    /// <summary>
    /// Restore command handler. Retores a data element to a given snapshot.
    /// </summary>
    [Command(
       Name = "restore",
       OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
       UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    public class Restore : IBaseCmd
    {
        /// <summary>
        /// Instance guid
        /// </summary>
        [Option(
            CommandOptionType.SingleValue,
            ShortName = "dg",
            LongName = "dataGuid",
            ShowInHelpText = true,
            Description = "DataGuid for the data element.")]
        [Guid]
        [Required]
        public string DataGuid { get; set; }

        /// <summary>
        /// Instance id.
        /// </summary>
        [Option(
            CommandOptionType.SingleValue,
            ShortName = "iid",
            LongName = "instanceId",
            ShowInHelpText = true,
            Description = "InstanceId [instanceOwner.partyId/instanceGuid] for the instance the dataElement is connected to.")]
        [InstanceId]
        public string InstanceId { get; set; }

        /// <summary>
        /// Instance guid
        /// </summary>
        [Option(
            CommandOptionType.SingleValue,
            ShortName = "ig",
            LongName = "instanceGuid",
            ShowInHelpText = true,
            Description = "InstanceGuid for the instance the dataElement is connected to.")]
        [Guid]
        public string InstanceGuid { get; set; }

        /// <summary>
        /// The organisation. 
        /// </summary>
        [Option(
          CommandOptionType.SingleValue,
          ShortName = "org",
          LongName = "organisation",
          ShowInHelpText = true,
          Description = "Organisation.")]
        [Required]
        public string Org { get; set; }

        /// <summary>
        /// The application. 
        /// </summary>
        [Option(
         CommandOptionType.SingleValue,
         ShortName = "app",
         LongName = "application",
         ShowInHelpText = true,
         Description = "Application.")]
        [Required]
        public string App { get; set; }

        /// <summary>
        /// The restore timestamp
        /// </summary>
        [Option(
         CommandOptionType.SingleValue,
         ShortName = "rt",
         LongName = "restoreTimestamp",
         ShowInHelpText = true,
         Description = "Restore timestamp.")]
        [Required]
        public string RestoreTimestamp { get; set; }

        private readonly ICosmosService _cosmosService;
        private readonly IBlobService _blobService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Restore"/> class.
        /// </summary>
        public Restore(ICosmosService cosmosService, IBlobService blobService)
        {
            _cosmosService = cosmosService;
            _blobService = blobService;
        }

        /// <inheritdoc/>
        protected override async Task OnExecuteAsync(CommandLineApplication app)
        {
            if (string.IsNullOrEmpty(Program.Environment))
            {
                Console.WriteLine("Please set the environment context before using this command.");
                Console.WriteLine("Update environment using cmd: settings update -e [environment] \n ");
                return;
            }

            if (string.IsNullOrEmpty(InstanceId) && string.IsNullOrEmpty(InstanceGuid))
            {
                Console.WriteLine("Please provide an instanceId or instanceGuid");
                return;
            }

            string instanceGuid = InstanceGuid ?? InstanceId.Split('/')[1];

            try
            {
                if (await _blobService.RestoreBlob(Org, App, instanceGuid, DataGuid, RestoreTimestamp))
                {
                    DataElement backup = await _blobService.GetDataElementBackup(instanceGuid, DataGuid, RestoreTimestamp);
                    if (backup != null && await _cosmosService.ReplaceDataElement(backup))
                    {
                        Console.WriteLine("-----------------------------------------------------------------------");
                        Console.WriteLine($"Restore successful. Data element restored.");
                        Console.WriteLine(JsonConvert.SerializeObject(backup, Formatting.Indented));
                        Console.WriteLine("-----------------------------------------------------------------------");
                    }
                    else
                    {
                        Console.WriteLine("-----------------------------------------------------------------------");
                        Console.WriteLine($"Restore was unsuccessful. Data element was not fully restored.");
                        Console.WriteLine($"Error occured when retrieving backup and writing metadata to cosmos for");
                        Console.WriteLine($"Please check state manually for: {Org}/{App}/{instanceGuid}/{DataGuid}. ");
                        Console.WriteLine("-----------------------------------------------------------------------");
                    }
                }
                else
                {
                    Console.WriteLine("-----------------------------------------------------------------------");
                    Console.WriteLine($"Restore was unsuccessful. Data element was not restored.");
                    Console.WriteLine($"Error occured when retrieving snapshot of data element in blob storage.");
                    Console.WriteLine($"Please check state manually for: {Org}/{App}/{instanceGuid}/{DataGuid}. ");
                    Console.WriteLine("-----------------------------------------------------------------------");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
