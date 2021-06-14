namespace RepoCleanup.Application.Commands
{
    public class CommitChangesCommand
    {
        public CommitChangesCommand(string localPath)
        {
            LocalPath = localPath;
        }

        public string LocalPath { get; private set; }
    }
}
