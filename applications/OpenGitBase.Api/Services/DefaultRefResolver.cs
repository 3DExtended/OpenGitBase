using OpenGitBase.Api.Models;

namespace OpenGitBase.Api.Services;

public static class DefaultRefResolver
{
    public const string DefaultBranchPatternAlias = "@default";

    public static string? Resolve(
        IReadOnlyList<RepositoryContentRefDto> branches,
        string? storedDefaultBranchName = null
    )
    {
        if (!string.IsNullOrWhiteSpace(storedDefaultBranchName))
        {
            var stored = branches.FirstOrDefault(branch =>
                string.Equals(branch.Name, storedDefaultBranchName, StringComparison.OrdinalIgnoreCase)
            );
            if (stored is not null)
            {
                return stored.Name;
            }

            return storedDefaultBranchName;
        }

        return Resolve(branches);
    }

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
