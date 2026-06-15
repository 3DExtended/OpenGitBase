using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class GitSessionProxyServiceTests
{
    [Theory]
    [InlineData(RepositoryOperation.ReadGit, "/srv/git/abc.git", "git-upload-pack '/srv/git/abc.git'")]
    [InlineData(RepositoryOperation.WriteGit, "/srv/git/abc.git", "git-receive-pack '/srv/git/abc.git'")]
    public void BuildGitCommand_MapsOperationToCanonicalPath(
        RepositoryOperation operation,
        string physicalPath,
        string expectedCommand
    )
    {
        var command = GitSessionProxyService.BuildGitCommand(operation, physicalPath);
        Assert.Equal(expectedCommand, command);
    }

    [Fact]
    public void BuildGitCommand_WhenUnknownOperation_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GitSessionProxyService.BuildGitCommand(RepositoryOperation.Unknown, "/srv/git/a.git")
        );
    }
}
