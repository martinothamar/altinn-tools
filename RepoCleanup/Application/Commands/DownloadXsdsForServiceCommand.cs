using RepoCleanup.Models;
using RepoCleanup.Utils;

namespace RepoCleanup.Application.Commands
{
    public class DownloadXsdsForServiceCommand
    {
        public DownloadXsdsForServiceCommand(Altinn2Service service, string localPath, NotALogger logger)
        {
            Service = service;
            LocalPath = localPath;
            Logger = logger;
        }

        public Altinn2Service Service { get; private set; }

        public string LocalPath { get; private set; }

        public NotALogger Logger { get; private set; }
    }
}
