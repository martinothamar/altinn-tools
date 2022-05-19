using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Platform.Storage.Interface.Models;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace FindEmptyInstances
{
    public class Program
    {
        private static string DatabaseId { get; set; } = "Storage";

        protected static Uri DatabaseUri { get; set; }

        private static Container InstanceContainer { get; set; }

        private static Container DataElementsContainer { get; set; }

        private static Container InstanceEventsContainer { get; set; }

        static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("Search for \"empty\" instances.");

            SetUpContainers();

            Console.WriteLine();

            await Run();
        }

        private static async Task Run()
        {
            IQueryable<Instance> instanceQueryBuilder = InstanceContainer.GetItemLinqQueryable<Instance>()
                .Where(i => i.Created > new DateTime(2022, 4, 25, 6, 0, 0))
                .Where(i => i.Created < new DateTime(2022, 4, 25, 15, 0, 0))
                .Where(i => i.Status.IsHardDeleted == false);

            List<Instance> createdInstances = await ReadItems(instanceQueryBuilder);

            Console.WriteLine($"We found {createdInstances.Count} created instances.");
            Console.WriteLine();

            List<Instance> emptyInstances = new();

            foreach (var createdInstance in createdInstances)
            {
                Console.Write(".");

                QueryRequestOptions dataElementsQueryRequestOptions = new()
                {
                    MaxBufferedItemCount = 0,
                    MaxConcurrency = -1,
                    PartitionKey = new(createdInstance.Id),
                    MaxItemCount = 10
                };

                IQueryable<DataElement> dataElementsQueryBuilder =
                    DataElementsContainer.GetItemLinqQueryable<DataElement>(requestOptions: dataElementsQueryRequestOptions);

                List<DataElement> dataElements = await ReadItems(dataElementsQueryBuilder);

                if (dataElements.Count > 0)
                {
                    continue;
                }

                emptyInstances.Add(createdInstance);
            }

            Console.WriteLine();
            Console.WriteLine($"There are {emptyInstances.Count} with no data elements.");
            Console.WriteLine();

            int dateTimeLength = DateTime.UtcNow.ToString("u").Length;
            int guidLength = Guid.NewGuid().ToString().Length;

            Console.Write("Org".PadRight(10, ' '));
            Console.Write("AppId".PadRight(40, ' '));
            Console.Write("Created".PadRight(dateTimeLength + 1, ' '));
            Console.Write("Deleted".PadRight("Deleted".Length + 1, ' '));
            Console.Write("Id".PadRight(guidLength + 1, ' '));
            Console.WriteLine();

            foreach (var instance in emptyInstances)
            {
                Console.Write(instance.Org.PadRight(10, ' '));
                Console.Write(instance.AppId.Split('/')[1].PadRight(40, ' '));
                Console.Write(instance.Created.Value.ToString("u").PadRight(dateTimeLength + 1, ' '));
                Console.Write(instance.Status.IsSoftDeleted.ToString().PadRight("Deleted".Length + 1, ' '));
                Console.Write(instance.Id.ToString().PadRight(guidLength + 1, ' '));
                Console.WriteLine();
            }
        }

        private static async Task<List<T>> ReadItems<T>(IQueryable<T> itemQueryBuilder)
        {
            List<T> items = new List<T>();

            using FeedIterator<T> setIterator = itemQueryBuilder.ToFeedIterator();

            while (setIterator.HasMoreResults)
            {
                foreach (var item in await setIterator.ReadNextAsync())
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private static void SetUpContainers()
        {
            string endpointUri = GetEndpointUri();
            string primaryKey = GetPrimmaryKey();

            CosmosClientOptions options = new()
            {
                ConnectionMode = ConnectionMode.Gateway
            };

            CosmosClient client = new CosmosClient(endpointUri, primaryKey, options);

            InstanceContainer = client.GetContainer(DatabaseId, "instances");
            DataElementsContainer = client.GetContainer(DatabaseId, "dataElements");
            InstanceEventsContainer = client.GetContainer(DatabaseId, "instanceEvents");
        }

        private static string GetEndpointUri()
        {
            Console.WriteLine();
            Console.WriteLine("The application needs the URL for the Cosmos DB service.");
            Console.Write("Provide URL: ");

            string uri = Console.ReadLine().Trim();

            return uri;
        }

        private static string GetPrimmaryKey()
        {
            Console.WriteLine();
            Console.WriteLine("The application needs a key to use in authentication.");
            Console.Write("Provide Key: ");

            string uri = Console.ReadLine().Trim();

            return uri;
        }

        private static string SelectInstanceOwner()
        {
            Console.WriteLine();
            Console.WriteLine("The search must be limited to one instance owner.");
            Console.Write("Provide party id: ");

            string partyId = Console.ReadLine().Trim();

            return partyId;
        }
    }
}
