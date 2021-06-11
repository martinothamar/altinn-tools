using RepoCleanup.Application.Commands;

using LibGit2Sharp;

namespace RepoCleanup.Application.CommandHandlers
{
    public class CloneGitRepositoryCommandHandler
    {
        public static void Handle(CloneGitRepositoryCommand command)
        {
            var cloneOptions = new CloneOptions
            {
                CredentialsProvider = (a, b, c) => new UsernamePasswordCredentials
                {
                    Username = Globals.GiteaToken,
                    Password = string.Empty
                }
            };

            Repository.Clone(command.RemotePath, command.LocalPath, cloneOptions);
        }
    }
}
