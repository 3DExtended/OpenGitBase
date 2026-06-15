using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class GitCommandParserTests
{
    private readonly GitCommandParser _parser = new();

    [Theory]
    [InlineData("git-upload-pack 'alice/repo'", RepositoryOperation.ReadGit, "alice/repo")]
    [InlineData("git-receive-pack \"alice/repo.git\"", RepositoryOperation.WriteGit, "alice/repo")]
    public void TryParse_ParsesGitCommands(
        string command,
        RepositoryOperation expectedOperation,
        string expectedPath
    )
    {
        var parsed = _parser.TryParse(command, out var operation, out var repositoryPath);

        Assert.True(parsed);
        Assert.Equal(expectedOperation, operation);
        Assert.Equal(expectedPath, repositoryPath);
    }
}
