using System.Collections.Generic;

namespace RepoCleanup.Application.Commands
{
    public class MigrateAltinn2FormSchemasCommand
    {
        public MigrateAltinn2FormSchemasCommand(List<string> organisations, string workPath, bool dryRun = true)
        {
            Organisations = organisations;
            WorkPath = workPath;
            DryRun = dryRun;
        }

        public List<string> Organisations { get; private set; }

        public string WorkPath { get; private set; }

        /// <summary>
        /// If set to true the command won't commit and push changes to remote Altinn 2 repos,
        /// just leave the files in local working folder.
        /// </summary>
        public bool DryRun { get; private set; }
    }
}
