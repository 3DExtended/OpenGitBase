using OpenGitBase.Api.Models;

namespace OpenGitBase.Api.Services;

public static class DirectoryEntrySorter
{
    public static IReadOnlyList<RepositoryContentEntryDto> Sort(
        IEnumerable<RepositoryContentEntryDto> entries
    )
    {
        return entries
            .OrderBy(entry => entry.Type == "tree" ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
