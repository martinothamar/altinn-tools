using System;
using System.Threading.Tasks;

using RepoCleanup.Application.Commands;
using RepoCleanup.Services;
using RepoCleanup.Models;

using LibGit2Sharp;

namespace RepoCleanup.Application.CommandHandlers
{
    public class CommitChangesCommandHandler
    {
        private readonly GiteaService _giteaService;

        public CommitChangesCommandHandler(GiteaService giteaService)
        {
            _giteaService = giteaService;
        }

        public async Task Handle(CommitChangesCommand command)
        {
            using (var repo = new LibGit2Sharp.Repository(command.LocalPath))
            {
                LibGit2Sharp.Commands.Stage(repo, "*");

                GiteaService giteaService = new GiteaService();

                User user = await _giteaService.GetAuthenticatedUser();
                Signature author = new Signature(user.Username, "@jugglingnutcase", DateTime.Now);

                Commit commit = repo.Commit("Added XSD schemas copied from Altinn II", author, author);
            }
        }
    }
}
