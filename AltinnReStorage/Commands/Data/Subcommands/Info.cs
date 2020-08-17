using System;
using System.Collections.Generic;
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
    /// Info command handler. Returns metadata about a data element.
    /// </summary>
    [Command(
      Name = "info",
      OptionsComparison = StringComparison.InvariantCultureIgnoreCase,
      UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue)]
    public class Info : IBaseCmd
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

        /// <summary>
        /// Boolean to include metadata from cosmos.
        /// </summary>
        [Option(
          CommandOptionType.NoValue,
          ShortName = "em",
          LongName = "exclude-metadata",
          ShowInHelpText = true,
          Description = "Exclude metadata from Cosmos DB.")]
        public bool ExcludeMetadata { get; set; }

        /// <summary>
        /// Boolean to include previous versions of the blob.
        /// </summary>
        [Option(
          CommandOptionType.NoValue,
          ShortName = "lv",
          LongName = "list-versions",
          ShowInHelpText = true,
          Description = "List versions history of the data element.")]
        public bool ListVersions { get; set; }

        private readonly IBlobService _blobService;
        private readonly ICosmosService _cosmosService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Info"/> class.
        /// </summary>
        public Info(ICosmosService cosmosService, IBlobService blobService)
        {
            _cosmosService = cosmosService;
            _blobService = blobService;
        }

        /// <summary>
        /// Retrieves metadata about the dataElement
        /// </summary>
        protected override async Task OnExecuteAsync(CommandLineApplication app)
        {
            if (string.IsNullOrEmpty(Program.Environment))
            {
                Console.WriteLine("Please set the environment context before using this command.");
                Console.WriteLine("Update environment using cmd: settings update -e [environment] \n ");
                return;
            }

            string instanceGuid = string.Empty;

            if (!string.IsNullOrEmpty(InstanceGuid) || !string.IsNullOrEmpty(InstanceId))
            {
                instanceGuid = InstanceGuid ?? InstanceId.Split('/')[1];
            }

            if (ListVersions && (string.IsNullOrEmpty(Org) || string.IsNullOrEmpty(App) || string.IsNullOrEmpty(instanceGuid)))
            {
                Console.WriteLine("Please provide org, app and instanceGuid to retrieve previous versions when listing deleted data elements.");
                return;
            }

            if (!ExcludeMetadata)
            {
                string metadata = await GetMetadata(DataGuid, instanceGuid);
                metadata = string.IsNullOrEmpty(metadata) ? "No metadata found." : metadata;
                Console.WriteLine("-----------------------------------------------------------------------");
                Console.WriteLine($"Metadata for data element: {instanceGuid}/{DataGuid}");
                Console.WriteLine("-----------------------------------------------------------------------");
                Console.WriteLine(metadata);
                Console.WriteLine(string.Empty);
            }

            if (ListVersions)
            {
                List<string> versions = await GetVersions(Org, App, instanceGuid, DataGuid);
                Console.WriteLine("-----------------------------------------------------------------------");
                Console.WriteLine($"Versions of data element: {instanceGuid}/{DataGuid}");
                Console.WriteLine("-----------------------------------------------------------------------");
                if (versions.Count == 0)
                {
                    Console.WriteLine($"No version history found.");
                }
                else
                {
                    foreach (string version in versions)
                    {
                        Console.WriteLine(version);
                    }
                }

                Console.WriteLine(string.Empty);
            }

            CleanUp();
        }

        private async Task<string> GetMetadata(string dataGuid, string instanceGuid)
        {
            DataElement element = await _cosmosService.GetDataElement(dataGuid, instanceGuid);
            return element == null ? string.Empty : JsonConvert.SerializeObject(element, Formatting.Indented);
        }

        private async Task<List<string>> GetVersions(string org, string app, string instanceGuid, string dataGuid)
        {
            List<string> versions = await _blobService.ListBlobVersions(org, app, instanceGuid, dataGuid);

            return versions;
        }

        private void CleanUp()
        {
            InstanceId = InstanceGuid = null;
            ExcludeMetadata = false;
            ListVersions = false;
        }
    }
}
