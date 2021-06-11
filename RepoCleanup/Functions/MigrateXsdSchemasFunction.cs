using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using LibGit2Sharp;

using RepoCleanup.Application.Commands;
using RepoCleanup.Application.CommandHandlers;
using RepoCleanup.Models;
using RepoCleanup.Services;
using RepoCleanup.Utils;

namespace RepoCleanup.Functions
{
    public static class MigrateXsdSchemasFunction
    {
        public static async Task Run()
        {
            NotALogger logger = new NotALogger("MigrateXsdSchemas - Log.txt");
            logger.AddNothing();

            SharedFunctionSnippets.WriteHeader("Migrating XSD Schemas from active services in Altinn II");

            string basePath = CollectMigrationWorkFolder();
            List<string> organisations = await CollectOrgInfo();

            logger.AddInformation($"Started!");
            logger.AddInformation($"Using '{basePath}' as base path for all organisations");
            logger.AddNothing();

            List<Altinn2Service> allReportingServices = await AltinnServiceRepository.GetReportingServices();

            foreach (string organisation in organisations)
            {
                List<Altinn2Service> organisationReportingServices = 
                    allReportingServices.Where(s => s.ServiceOwnerCode.ToLower() == organisation).ToList();

                string orgFolder = $"{basePath}\\{organisation}";
                Directory.CreateDirectory(orgFolder);

                string repoName = $"{organisation}-datamodels";
                string repoFolder = $"{orgFolder}\\{repoName}";

                if (!Directory.Exists(repoFolder))
                {
                    CloneGitRepositoryCommand cloneGitRepositoryCommand = new CloneGitRepositoryCommand(
                        $"{Globals.RepositoryBaseUrl}/{organisation}/{repoName}.git",
                        repoFolder);
                    CloneGitRepositoryCommandHandler.Handle(cloneGitRepositoryCommand);
                }

                Directory.CreateDirectory($"{repoFolder}\\.altinnstudio");
                Directory.CreateDirectory($"{repoFolder}\\altinn2");
                Directory.CreateDirectory($"{repoFolder}\\shared");

                await System.IO.File.WriteAllTextAsync(
                    $"{repoFolder}\\.altinnstudio\\settings.json", 
                    "{\n  \"repotype\" : \"datamodels\"\n}");

                await System.IO.File.WriteAllTextAsync(
                    $"{repoFolder}\\shared\\README.md",
                    "# Shared models");

                foreach (Altinn2Service service in organisationReportingServices)
                {
                    logger.AddNothing();
                    logger.AddInformation($"Service: {service.ServiceName}");

                    Altinn2ReportingService reportingService = await AltinnServiceRepository.GetReportingService(service);

                    string serviceName = $"{service.ServiceCode}-{service.ServiceEditionCode}" 
                        + $"-{service.ServiceName.AsFileName(false).Trim(' ', '.', ',')}";

                    string serviceDirectory = $"{repoFolder}\\altinn2\\{serviceName}";

                    Directory.CreateDirectory(serviceDirectory);

                    foreach (Altinn2Form formMetaData in reportingService.FormsMetaData)
                    {
                        XDocument xsdDocument = await AltinnServiceRepository.GetFormXsd(service, formMetaData);
                        if (xsdDocument == null)
                        {
                            continue;
                        }

                        string fileName = $"{serviceDirectory}\\{formMetaData.DataFormatID}-{formMetaData.DataFormatVersion}.xsd";
                        using (FileStream fileStream = new FileStream(fileName, FileMode.OpenOrCreate))
                        {
                            await xsdDocument.SaveAsync(fileStream, SaveOptions.None, CancellationToken.None);
                        }
                    }
                }

                List<string> changedFiles = Status(repoFolder);

                if (changedFiles.Count > 0)
                {
                    await Commit(repoFolder);
                    await Push(organisation, repoName, repoFolder);
                }
            }

            logger.WriteLog();
            await Task.Delay(1);

            return;
        }

        private static string CollectMigrationWorkFolder()
        {
            Console.WriteLine("This operation requires a folder to which it can clone the datamodels repositories.");
            string basePath = SharedFunctionSnippets.CollectInput("Provide folder name (must exist): ");

            if (!Directory.Exists(basePath))
            {
                Console.WriteLine("Can't find the specified folder!");
                return CollectMigrationWorkFolder();
            }

            return basePath;
        }

        /// <summary>
        /// Commit changes for repository
        /// </summary>
        private static async Task Commit(string localRepoPath)
        {
            using (LibGit2Sharp.Repository repo = new LibGit2Sharp.Repository(localRepoPath))
            {
                Commands.Stage(repo, "*");

                GiteaService giteaService = new GiteaService();

                User user = await giteaService.GetAuthenticatedUser();
                Signature author = new Signature(user.Username, "@jugglingnutcase", DateTime.Now);

                Commit commit = repo.Commit("Added XSD schemas copied from Altinn II", author, author);
            }
        }

        /// <summary>
        /// Push commits to repository
        /// </summary>
        /// <param name="org">Unique identifier of the organisation responsible for the app.</param>
        /// <param name="repository">The name of the repository</param>
        private static async Task<bool> Push(string org, string repository, string localRepoPath)
        {
            bool pushSuccess = true;
            using (LibGit2Sharp.Repository repo = new LibGit2Sharp.Repository(localRepoPath))
            {
                string remoteRepo = $"{Globals.RepositoryBaseUrl}/{org}/{repository}";
                Remote remote = repo.Network.Remotes["origin"];

                if (!remote.PushUrl.Equals(remoteRepo))
                {
                    // This is relevant when we switch beteen running designer in local or in docker. The remote URL changes.
                    // Requires adminstrator access to update files.
                    repo.Network.Remotes.Update("origin", r => r.Url = remoteRepo);
                }

                PushOptions options = new PushOptions
                {
                    OnPushStatusError = pushError =>
                    {
                        pushSuccess = false;
                    }
                };
                options.CredentialsProvider = (_url, _user, _cred) =>
                        new UsernamePasswordCredentials { Username = Globals.GiteaToken, Password = string.Empty };

                repo.Network.Push(remote, @"refs/heads/master", options);
            }

            return await Task.FromResult(pushSuccess);
        }



        /// <summary>
        /// List the GIT status of a repository
        /// </summary>
        /// <param name="org">Unique identifier of the organisation responsible for the app.</param>
        /// <param name="repository">The name of the repository</param>
        /// <returns>A list of changed files in the repository</returns>
        public static List<string> Status(string localRepoPath)
        {
            List<string> repoContent = new List<string>();
            using (var repo = new LibGit2Sharp.Repository(localRepoPath))
            {
                RepositoryStatus status = repo.RetrieveStatus(new StatusOptions());
                foreach (StatusEntry item in status)
                {
                    repoContent.Add(item.FilePath);
                }
            }

            return repoContent;
        }

        private static async Task<List<string>> CollectOrgInfo()
        {
            List<string> orgs = new List<string>();

            bool updateAllOrgs = CheckIfAllOrgs();

            if (updateAllOrgs)
            {
                List<Organisation> organisations = await GiteaService.GetOrganisations();
                orgs.AddRange(organisations.Select(o => o.Username));
            }
            else
            {
                Console.Write("\r\nProvide organisation name: ");

                string name = Console.ReadLine();
                orgs.Add(name.ToLower());
            }

            return orgs;
        }

        private static bool CheckIfAllOrgs()
        {
            Console.Write("\r\nShould the team be created for all organisations? (Y)es / (N)o: ");
            
            switch (Console.ReadLine().ToUpper())
            {
                case "Y":
                    return true;
                case "N":
                    return  false;
                default:
                    return CheckIfAllOrgs();
            }
        }
    }
}
