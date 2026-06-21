using OpenGitBase.Api.Models;

namespace OpenGitBase.Api.Services;

public static class DefaultRefResolver
{
    public static string? Resolve(IReadOnlyList<RepositoryContentRefDto> branches)
    {
        if (branches.Count == 0)
        {
            return null;
        }

        var main = branches.FirstOrDefault(branch =>
            string.Equals(branch.Name, "main", StringComparison.OrdinalIgnoreCase)
        );
        if (main is not null)
        {
            return main.Name;
        }

        var master = branches.FirstOrDefault(branch =>
            string.Equals(branch.Name, "master", StringComparison.OrdinalIgnoreCase)
        );
        if (master is not null)
        {
            return master.Name;
        }

        return branches.OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase).First().Name;
    }
}
