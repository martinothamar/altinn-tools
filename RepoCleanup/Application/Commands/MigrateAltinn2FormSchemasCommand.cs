using RepoCleanup.Utils;
using System.Collections.Generic;

namespace RepoCleanup.Application.Commands
{
    public class MigrateAltinn2FormSchemasCommand
    {
        public MigrateAltinn2FormSchemasCommand(List<string> organisations, string workPath)
        {
            Organisations = organisations;
            WorkPath = workPath;
        }

        public List<string> Organisations { get; private set; }

        public string WorkPath { get; private set; }
    }
}
