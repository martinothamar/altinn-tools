using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RepoCleanup
{
    static class Globals
    {
        public static HttpClient Client { set; get; }

        public static bool IsDryRun { get; set; } = true;

        public static JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }
}
