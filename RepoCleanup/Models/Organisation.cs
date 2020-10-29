using System.Text.Json.Serialization;

namespace RepoCleanup.Models
{
    public class Organisation
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }
}
