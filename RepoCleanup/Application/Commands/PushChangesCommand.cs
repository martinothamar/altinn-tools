namespace RepoCleanup.Application.Commands
{
    public class PushChangesCommand
    {
        public PushChangesCommand(string remotePath, string localPath, string token)
        {
            RemotePath = remotePath;
            LocalPath = localPath;
            Token = token;
        }

        public string RemotePath { get; private set; }

        public string LocalPath { get; private set; }

        public string Token { get; private set; }
    }
}
