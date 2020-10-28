using System.Net.Http;

namespace RepoCleanup
{
    static class Globals
    {
        public static HttpClient Client { set; get; }

        public static bool IsDryRun { get; set; } = true;
    }
}
