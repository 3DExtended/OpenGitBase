using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class DirectoryEntrySorterTests
{
    [Fact]
    public void Sort_PlacesDirectoriesBeforeFiles()
    {
        var entries = new List<RepositoryContentEntryDto>
        {
            new() { Name = "README.md", Path = "README.md", Type = "blob" },
            new() { Name = "src", Path = "src", Type = "tree" },
            new() { Name = "docs", Path = "docs", Type = "tree" },
            new() { Name = "LICENSE", Path = "LICENSE", Type = "blob" },
        };

        var sorted = DirectoryEntrySorter.Sort(entries);

        Assert.Equal(["docs", "src", "LICENSE", "README.md"], sorted.Select(entry => entry.Name));
    }

    [Fact]
    public void Sort_SortsNamesAlphabeticallyWithinSameType()
    {
        var entries = new List<RepositoryContentEntryDto>
        {
            new() { Name = "zebra", Path = "zebra", Type = "blob" },
            new() { Name = "alpha", Path = "alpha", Type = "blob" },
            new() { Name = "Beta", Path = "Beta", Type = "blob" },
        };

        var sorted = DirectoryEntrySorter.Sort(entries);

        Assert.Equal(["alpha", "Beta", "zebra"], sorted.Select(entry => entry.Name));
    }
}
