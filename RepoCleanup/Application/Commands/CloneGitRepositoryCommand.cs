namespace RepoCleanup.Application.Commands
{
    public class CloneGitRepositoryCommand
    {
        public CloneGitRepositoryCommand(string remotePath, string localPath)
        {
            RemotePath = remotePath;
            LocalPath = localPath;
        }

        public string RemotePath { get; private set; }

        public string LocalPath { get; private set; }
    }
}
