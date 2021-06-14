using RepoCleanup.Application.Commands;

using LibGit2Sharp;

namespace RepoCleanup.Application.CommandHandlers
{
    public class PushChangesCommandHandler
    {
        public static void Handle(PushChangesCommand command)
        {
            using (Repository repo = new Repository(command.LocalPath))
            {
                Remote remote = repo.Network.Remotes["origin"];

                PushOptions options = new PushOptions
                {
                    CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials 
                    { 
                        Username = command.Token, 
                        Password = string.Empty 
                    }
                };

                repo.Network.Push(remote, @"refs/heads/master", options);
            }
        }
    }
}
