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

            MigrateAltinn2FormSchemasCommandHandler migrateAltinn2FormSchemasCommandHandler = new(new GiteaService(), logger);
            MigrateAltinn2FormSchemasCommand migrateAltinn2FormSchemasCommand = new(organisations, basePath);
            await migrateAltinn2FormSchemasCommandHandler.Handle(migrateAltinn2FormSchemasCommand);

            logger.WriteLog();

            return;
        }

        private static string CollectMigrationWorkFolder()
        {
            Console.WriteLine("This operation requires a folder to which it can clone the datamodels repositories.");
            string basePath = SharedFunctionSnippets.CollectInput("Provide folder name (should be empty): ");

            if (!Directory.Exists(basePath))
            {
                Console.WriteLine("Can't find the specified folder!");
                return CollectMigrationWorkFolder();
            }

            return basePath;
        }

        private static async Task<List<string>> CollectOrgInfo()
        {
            List<string> orgs = new();

            bool updateAllOrgs = SharedFunctionSnippets.ShouldThisApplyToAllOrgs();

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
    }
}
