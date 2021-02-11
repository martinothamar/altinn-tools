using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RepoCleanup.Models;
using RepoCleanup.Services;
using RepoCleanup.Utils;

namespace RepoCleanup.Functions
{
    public static class CreateOrgWithTeams
    {
        public static async Task Run()
        {
            StringBuilder logBuilder = new StringBuilder();
            string logName = @$"CreateOrgWithTeams-Log.txt";
            bool createdOrg = false;

            Console.Clear();
            Console.WriteLine("\r\n----------------------------------------------------------------");
            Console.WriteLine("------------ Create new organisation with teams ----------------");
            Console.WriteLine("----------------------------------------------------------------");

            Organisation org = CollectOrgInfo();

            Console.WriteLine("Creating org. Please wait.");
            GiteaResponse response = await GiteaService.CreateOrg(org);

            if (response.Success)
            {
                logBuilder.AppendLine($"{DateTime.Now} - Information - Org {org.Username} was successfully added to Gitea.");
                createdOrg = true;
            }
            else
            {
                logBuilder.AppendLine($"{DateTime.Now} - Error - Org {org.Username} was not added to Gitea.");
                logBuilder.AppendLine($"{DateTime.Now} - Error -  {response.StatusCode}:{JsonSerializer.Serialize(response.ResponseMessage, Globals.SerializerOptions)}.");
            }

            using (StreamWriter file = new StreamWriter(logName, true))
            {
                file.WriteLine(logBuilder.ToString());
            }

            logBuilder.Clear();

            if (createdOrg)
            {
                Console.WriteLine($"Org {org.Username} successfully created.");
            }
            else
            {
                Console.WriteLine($"Create org for {org.Username} failed. See log {logName}.");
            }

            bool createdTeams = true;
            List<CreateTeamOption> teams = new List<CreateTeamOption>();
            teams.Add(TeamOption.GetCreateTeamOption("Deploy-Prod", "Members can deploy to production", false, Permission.read));
            teams.Add(TeamOption.GetCreateTeamOption("Deploy-TT02", "Members can deploy to TT02", false, Permission.read));
            teams.Add(TeamOption.GetCreateTeamOption("Devs", "All application developers", true, Permission.write));

            Console.WriteLine("Creating teams for org. Please wait.");
            foreach (CreateTeamOption team in teams)
            {
                response = await GiteaService.CreateTeam(org.Username, team);

                if (response.Success)
                {
                    logBuilder.AppendLine($"{DateTime.Now} - Information - Team {team.Name} was successfully added to org: {org.Username}.");
                }
                else
                {
                    createdTeams = false;
                    logBuilder.AppendLine($"{DateTime.Now} - Error - Team {team.Name} was not added to org {org.Username}.");
                    logBuilder.AppendLine($"{DateTime.Now} - Error -  {response.StatusCode}:{JsonSerializer.Serialize(response.ResponseMessage, Globals.SerializerOptions)}.");
                }
            }

            if (createdTeams)
            {
                Console.WriteLine($"All teams for {org.Username} successfully created.");
            }
            else
            {
                Console.WriteLine($"Create teams for {org.Username} failed. See log {logName}.");
            }
        }

        private static Organisation CollectOrgInfo()
        {
            bool isValid = false;
            string username = string.Empty;

            while (!isValid)
            {
                Console.Write("\r\nSet username (shortname) for org: ");
                username = Console.ReadLine().ToLower();

                isValid = Regex.IsMatch(username, "^[a-z]+[a-z0-9\\-]+[a-z0-9]$");
                if (!isValid)
                {
                    Console.WriteLine("Invalid name. Letters a-z and character '-' are permitted. Username must start with a letter and end with a letter or number.");
                }
            }

            string fullname = string.Empty;

            Console.Write("\r\nSet fullname for org: ");
            fullname = Console.ReadLine();

            isValid = false;
            string website = string.Empty;

            while (!isValid)
            {
                Console.Write("\r\nSet website for org: ");
                website = Console.ReadLine();

                if (string.IsNullOrEmpty(website))
                {
                    isValid = true;
                }
                else
                {
                    isValid = Regex.IsMatch(website, "^[a-zA-Z0-9\\-._/:]*$");
                }
                if (!isValid)
                {
                    Console.WriteLine("Invalid website adress. Letters a-z and characters:'-', '_', '.', '/', ':' are permitted.");
                }
            }

            Organisation org = new Organisation();
            org.Username = username;
            org.Fullname = fullname;
            org.Website = website;
            org.Visibility = "public";
            org.RepoAdminChangeTeamAccess = false;
            return org;
        }
    }
}