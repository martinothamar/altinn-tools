using RepoCleanup.Models;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RepoCleanup.Services
{
    public static class GiteaService
    {
        public static async Task<List<Organisation>> GetOrganisations()
        {
            HttpResponseMessage res = await Globals.Client.GetAsync("admin/orgs");
            string jsonString = await res.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<Organisation>>(jsonString);
        }

        public static async Task<List<User>> GetUsers()
        {
            HttpResponseMessage res = await Globals.Client.GetAsync("admin/users");
            string jsonString = await res.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<User>>(jsonString);
        }

        public static async Task<List<File>> GetRepoContent(Repository repo)
        {
            HttpResponseMessage res = await Globals.Client.GetAsync($"repos/{repo.Owner.Username}/{repo.Name}/contents");
            string jsonString = await res.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<File>>(jsonString);
        }

        public static async Task<GiteaResponse> CreateTeam(string org, CreateTeamOption teamOption)
        {
            GiteaResponse result = new GiteaResponse();

            HttpContent content = new StringContent(JsonSerializer.Serialize(teamOption, Globals.SerializerOptions), Encoding.UTF8, "application/json");
            HttpResponseMessage res = await Globals.Client.PostAsync($"orgs/{org}/teams", content);

            if (res.StatusCode == System.Net.HttpStatusCode.Created)
            {
                result.Success = true;
                result.ResponseMessage = await res.Content.ReadAsStringAsync();
            }
            else
            {
                result.ResponseMessage = await res.Content.ReadAsStringAsync();
                result.Success = false;
                result.StatusCode = res.StatusCode;
            }

            return result;
        }
    }
}
