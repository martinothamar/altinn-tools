using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AltinnReStorage.Attributes;
using AltinnReStorage.Enums;
using AltinnReStorage.Services;
using Azure.Storage.Blobs.Models;
using McMaster.Extensions.CommandLineUtils;

namespace AltinnReStorage.Commands.Data
{
    /// <summary>
    /// List command handler. Lists data elements based on given parameters.
    /// </summary>
    [Command(
      Name = "list",
      OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
      UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    public class List : IBaseCmd
    {
        /// <summary>
        /// Instance Id.
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
        /// The state of the data element. 
        /// Active or deleted.
        /// </summary>
        [Option(
            CommandOptionType.SingleValue,
            ShortName = "ds",
            LongName = "dataState",
            ShowInHelpText = true,
            Description = "State of the data element. Active or deleted. Use All for both states.")]
        [AllowedValues("all", "active", "deleted", IgnoreCase = true)]
        public string DataState { get; set; } = "active";

        /// <summary>
        /// The organisation. 
        /// </summary>
        [Option(
          CommandOptionType.SingleValue,
          ShortName = "org",
          LongName = "organisation",
          ShowInHelpText = true,
          Description = "Organisation.")]
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
        public string App { get; set; }

        private readonly ICosmosService _cosmosService;
        private readonly IBlobService _blobService;

        /// <summary>
        /// Initializes a new instance of the <see cref="List"/> class.
        /// </summary>
        public List(ICosmosService cosmosService, IBlobService blobService)
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

            if ((DataState.Equals("deleted") || DataState.Equals("all")) && (string.IsNullOrEmpty(Org) || string.IsNullOrEmpty(App)))
            {
                Console.WriteLine("Please provide org and app when listing deleted data elements.");
                return;
            }

            string instanceGuid = InstanceGuid ?? InstanceId.Split('/')[1];

            switch (DataState.ToLower())
            {
                case "deleted":
                    await ListDataElements(Org, App, instanceGuid, ElementState.Deleted);
                    break;
                case "all":
                    await ListDataElements(Org, App, instanceGuid, ElementState.All);
                    break;
                case "active":
                default:
                    await ListActiveDataElements(instanceGuid);
                    break;
            }

            CleanUp();
        }

        private async Task ListDataElements(string org, string app, string instanceGuid, ElementState state)
        {
            List<BlobItem> blobs = await _blobService.ListBlobs(org, app, instanceGuid, state);

            if (blobs.Count == 0)
            {
                Console.WriteLine($"No data elements in state {DataState} found for instance {org}/{app}/{instanceGuid} in {Program.Environment}. \n");
                return;
            }

            Console.WriteLine($"Data elements for instanceGuid {instanceGuid}:");

            foreach (BlobItem item in blobs)
            {
                Console.WriteLine($"\t {item.Name.Split('/')[4]} \t Deleted:{item.Deleted} ");
            }

            Console.WriteLine(string.Empty);
        }

        private async Task ListActiveDataElements(string instanceGuid)
        {
            try
            {
                List<string> dataGuids = await _cosmosService.ListDataElements(instanceGuid);

                if (dataGuids.Count > 0)
                {
                    Console.WriteLine($"Active data elements for instanceGuid {instanceGuid}:");
                    foreach (string id in dataGuids)
                    {
                        Console.WriteLine("\t" + id);
                    }
                }
                else
                {
                    Console.WriteLine($"No active data elements were found for instanceGuid \"{instanceGuid}\"");
                }

                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void CleanUp()
        {
            InstanceId = InstanceGuid = Org = App = null;
            DataState = "active";
        }
    }
}
