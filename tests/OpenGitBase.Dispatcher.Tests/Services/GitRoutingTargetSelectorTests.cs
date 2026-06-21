using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class GitRoutingTargetSelectorTests
{
    [Fact]
    public void SelectWriteTarget_PrefersPrimaryTarget()
    {
        var accessCheck = new RepositoryAccessCheckResponse
        {
            Primary = new StorageRoutingTarget
            {
                InternalHost = "storage-1",
                InternalSshPort = 22,
                InternalGitHttpPort = 8082,
                Role = "Primary",
            },
            ReadTargets =
            [
                new StorageRoutingTarget
                {
                    InternalHost = "storage-2",
                    InternalSshPort = 22,
                    InternalGitHttpPort = 8082,
                    Role = "Replica",
                },
            ],
        };

        var target = GitRoutingTargetSelector.SelectWriteTarget(accessCheck);

        Assert.Equal("storage-1", target.InternalHost);
    }

    [Fact]
    public void SelectReadTarget_UsesFirstReadTarget()
    {
        var accessCheck = new RepositoryAccessCheckResponse
        {
            ReadTargets =
            [
                new StorageRoutingTarget
                {
                    InternalHost = "storage-1",
                    InternalSshPort = 22,
                    InternalGitHttpPort = 8082,
                    Role = "Primary",
                },
                new StorageRoutingTarget
                {
                    InternalHost = "storage-2",
                    InternalSshPort = 22,
                    InternalGitHttpPort = 8082,
                    Role = "Replica",
                },
            ],
        };

        var target = GitRoutingTargetSelector.SelectReadTarget(accessCheck);

        Assert.Equal("storage-1", target.InternalHost);
    }
}
