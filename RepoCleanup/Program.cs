using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RepoCleanup.Models;

namespace RepoCleanup
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("Altinn Studio Repository cleanup");
            SetUpClient();

            Console.Clear();
            Console.WriteLine("Getting organisations...");
            List<Organisation> orgs = await GetOrganisations();

            Console.WriteLine("Getting users...");
            List<User> users = await GetUsers();

            Console.Write("Getting repositories...");
            List<Repository> repos = new List<Repository>();
            foreach (Organisation org in orgs)
            {
                Console.Write(".");
                repos.AddRange(await GetRepositories(null, org.Username));
            }

            foreach (User user in users)
            {
                Console.Write(".");
                repos.AddRange(await GetRepositories(user.Username, null));
            }

            Console.WriteLine($"\r\nTotal number of repos:  {repos.Count}");

            Console.WriteLine($"Filtering repositories...");
            List<Repository> filtered = await FilterRepos(repos);

            Console.WriteLine($"Total number of repos to delete:  {filtered.Count}");

            Console.Write($"Deleting repositories...");
            foreach (Repository repository in filtered)
            {
                await DeleteRepository(repository);
            }

            Globals.Client.Dispose();
        }

        private static async Task<List<Organisation>> GetOrganisations()
        {
            HttpResponseMessage res = await Globals.Client.GetAsync("admin/orgs");
            string jsonString = await res.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<Organisation>>(jsonString);
        }

        private static async Task<List<User>> GetUsers()
        {
            HttpResponseMessage res = await Globals.Client.GetAsync("admin/users");
            string jsonString = await res.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<User>>(jsonString);
        }

        private static async Task<List<Repository>> GetRepositories(string username, string orgUsername)
        {
            string requestPath;
            if (!string.IsNullOrEmpty(orgUsername))
            {
                requestPath = $"orgs/{orgUsername}/repos";
            }
            else
            {
                requestPath = $"users/{username}/repos";
            }

            HttpResponseMessage res = await Globals.Client.GetAsync(requestPath);
            string jsonString = await res.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<Repository>>(jsonString);
        }

        private static async Task<List<File>> GetRepoContent(Repository repo)
        {
            HttpResponseMessage res = await Globals.Client.GetAsync($"repos/{repo.Owner.Username}/{repo.Name}/contents");
            string jsonString = await res.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<File>>(jsonString);
        }

        private static async Task DeleteRepository(Repository repo)
        {
            HttpResponseMessage res = await Globals.Client.DeleteAsync($"repos/{repo.Owner.Username}/{repo.Name}");
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"\r\n Delete {repo.Owner.Username}/{repo.Name} incomplete. Failed with status code {res.StatusCode}: {await res.Content.ReadAsStringAsync()}.");
            }
            else
            {
                Console.WriteLine($"\r\n Delete {repo.Owner.Username}/{repo.Name} completed.");
            }
        }

        private static async Task<List<Repository>> FilterRepos(List<Repository> repos)
        {

            List<Repository> filteredList = new List<Repository>();

            filteredList.AddRange(repos.Where(r => r.Created < new DateTime(2020, 1, 1) && r.Updated < new DateTime(2020, 1, 1)).ToList());

            filteredList.AddRange(repos.Where(r => r.Name.Equals("codelists")).ToList());

            filteredList = filteredList.Distinct().ToList();

            repos.RemoveAll(r => filteredList.Contains(r));
            foreach (Repository repo in repos)
            {
                List<File> files = await GetRepoContent(repo);
                if (files.Exists(f => f.Name.Equals("AltinnService.csproj")))
                {
                    filteredList.Add(repo);
                }
            }

            return filteredList;
        }

        private static void SetUpClient()
        {
            string url = string.Empty;
            Enums.Environment env = SelectEnvironment();

            switch (env)
            {
                case Enums.Environment.Development:
                    url = "https://dev.altinn.studio/repos/api/v1/";
                    break;
                case Enums.Environment.Staging:
                    url = "https://staging.altinn.studio/repos/api/v1/";
                    break;
                case Enums.Environment.Production:
                    url = "https://altinn.studio/repos/api/v1/";
                    break;
                default:
                    SelectEnvironment();
                    break;
            }

            string token = GetAccessToken(env);
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
            Globals.Client = client;
        }

        private static Enums.Environment SelectEnvironment()
        {
            Console.Clear();
            Console.WriteLine("Choose an environment:");
            Console.WriteLine("1) Development");
            Console.WriteLine("2) Staging");
            Console.WriteLine("3) Production");
            Console.Write("\r\nSelect an option: ");

            switch (Console.ReadLine())
            {
                case "1":
                    return Enums.Environment.Development;
                case "2":
                    return Enums.Environment.Staging;
                case "3":
                    return Enums.Environment.Production;
                default:
                    return Enums.Environment.Development;
            }
        }

        private static string GetAccessToken(Enums.Environment env)
        {
            string url = string.Empty;

            Console.Clear();
            Console.WriteLine("The application requires a valid API key with admin acces.");
            switch (env)
            {
                case Enums.Environment.Development:
                    url = "https://dev.altinn.studio/repos/user/settings/applications";
                    break;
                case Enums.Environment.Staging:
                    url = "https://staging.altinn.studio/repos/user/settings/applications";
                    break;
                case Enums.Environment.Production:
                    url = "https://ltinn.studio/repos/user/settings/applications";
                    break;

            }

            Console.WriteLine($"Tokens can be generated on this page: {url}");
            Console.Write("\r\nProvide token: ");

            string token = Console.ReadLine().Trim();
            if (token.Length != 40)
            {
                return GetAccessToken(env);
            }

            return token;
        }
    }
}
