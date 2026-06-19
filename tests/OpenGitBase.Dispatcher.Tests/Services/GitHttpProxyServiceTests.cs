using Microsoft.AspNetCore.Http;
using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class GitHttpProxyServiceTests
{
    [Fact]
    public void BuildStorageRelativePath_MapsPhysicalPathToGitHttpPath()
    {
        var gitRequest = new GitSmartHttpRequest
        {
            GitSuffix = "info/refs",
            Operation = RepositoryOperation.ReadGit,
            RepositoryPath = "alice/demo",
        };

        var relativePath = GitHttpProxyService.BuildStorageRelativePath(
            "/srv/git/71a7e007-c34e-4c09-9329-9e664b1b1708.git",
            gitRequest,
            new QueryString("?service=git-upload-pack")
        );

        Assert.Equal(
            "/71a7e007-c34e-4c09-9329-9e664b1b1708.git/info/refs?service=git-upload-pack",
            relativePath
        );
    }

    [Fact]
    public void BuildStorageRelativePath_MapsUploadPackPath()
    {
        var gitRequest = new GitSmartHttpRequest
        {
            GitSuffix = "git-upload-pack",
            Operation = RepositoryOperation.ReadGit,
            RepositoryPath = "alice/demo",
        };

        var relativePath = GitHttpProxyService.BuildStorageRelativePath(
            "/srv/git/demo-id.git",
            gitRequest,
            QueryString.Empty
        );

        Assert.Equal("/demo-id.git/git-upload-pack", relativePath);
    }
}
