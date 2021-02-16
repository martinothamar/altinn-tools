using RepoCleanup.Models;

namespace RepoCleanup.Utils
{
    public static class TeamOption
    {
        public static CreateTeamOption GetCreateTeamOption(string name, string description, bool canCreateOrgRepo, Permission permission)
        {
            CreateTeamOption teamOption = new CreateTeamOption
            {
                CanCreateOrgRepo = canCreateOrgRepo,
                Description = description,
                IncludesAllRepositories = true,
                Name = name,
                Permission = permission
            };

            return teamOption;
        }
    }
}