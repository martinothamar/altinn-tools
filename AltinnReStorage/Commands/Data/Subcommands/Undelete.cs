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
    /// Undelete command handler. Returns metadata about a data element.
    /// </summary>
    [Command(
      Name = "undelete",
      OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
      UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    public class Undelete : IBaseCmd
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

        private readonly ICosmosService _cosmosService;
        private readonly IBlobService _blobService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Undelete"/> class.
        /// </summary>
        public Undelete(ICosmosService cosmosService, IBlobService blobService)
        {
            _cosmosService = cosmosService;
            _blobService = blobService;
        }

        /// <summary>
        /// Undeletes the given data element
        /// </summary>
        protected override async Task OnExecuteAsync(CommandLineApplication app)
        {
            if (string.IsNullOrEmpty(Program.Environment))
            {
                Console.WriteLine("Please set the environment context before using this command.");
                Console.WriteLine("Update environment using cmd: settings update -e [environment] \n ");
                CleanUp();
                return;
            }

            string instanceGuid = string.Empty;

            if (!string.IsNullOrEmpty(InstanceGuid) || !string.IsNullOrEmpty(InstanceId))
            {
                instanceGuid = InstanceGuid ?? InstanceId.Split('/')[1];
            }

            if (string.IsNullOrEmpty(instanceGuid))
            {
                Console.WriteLine("One of the fields --instanceGuid or --instanceId are required.");
                CleanUp();
                return;
            }

            if (await _blobService.UndeleteBlob(Org, App, instanceGuid, DataGuid))
            {
                DataElement backup = await _blobService.GetDataElementBackup(instanceGuid, DataGuid);
                if (backup != null && await _cosmosService.SaveDataElement(backup))
                {
                    Console.WriteLine("-----------------------------------------------------------------------");
                    Console.WriteLine($"Undelete successful. Data element restored.");
                    Console.WriteLine(JsonConvert.SerializeObject(backup, Formatting.Indented));
                    Console.WriteLine("-----------------------------------------------------------------------");
                }
                else
                {
                    Console.WriteLine("-----------------------------------------------------------------------");
                    Console.WriteLine($"Undelete unsuccessful. Data element was not fully restored.");
                    Console.WriteLine($"Error occured when retrieving backup and writing metadata to cosmos for");
                    Console.WriteLine($"Please check state manually for: {Org}/{App}/{instanceGuid}/{DataGuid}. ");
                    Console.WriteLine("-----------------------------------------------------------------------");
                }
            }
            else
            {
                Console.WriteLine("-----------------------------------------------------------------------");
                Console.WriteLine($"Undelete unsuccessful. Data element was not restored.");
                Console.WriteLine($"Error occured when undeleting data element in blob storage.");
                Console.WriteLine($"Please check state manually for: {Org}/{App}/{instanceGuid}/{DataGuid}. ");
                Console.WriteLine("-----------------------------------------------------------------------");
            }

            CleanUp();
        }

        private void CleanUp()
        {
            DataGuid = InstanceId = InstanceGuid = Org = App = null;
        }
    }
}
