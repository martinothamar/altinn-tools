using System.Net.Http;

namespace RepoCleanup
{
    static class Globals
    {
               private static HttpClient _client;

        public static HttpClient Client
        {
            set => _client = value;
            get { return _client; }
        }

    }
}
