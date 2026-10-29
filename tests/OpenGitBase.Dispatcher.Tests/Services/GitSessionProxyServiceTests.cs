using System.Runtime.CompilerServices;
using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Services;
using Renci.SshNet;

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

    [Fact]
    public async Task ForwardInputAsync_WhenClientSendsNothing_SendsEof()
    {
        var remoteInput = new MemoryStream();
        var eofsSent = 0;

        await GitSessionProxyService.ForwardInputAsync(
            new MemoryStream(),
            remoteInput,
            receivePackPrefix: null,
            sendEof: () => eofsSent++,
            CancellationToken.None
        );

        Assert.Empty(remoteInput.ToArray());
        Assert.Equal(1, eofsSent);
    }

    [Fact]
    public async Task ForwardInputAsync_WhenClientSendsData_LeavesEofToTheStream()
    {
        var input = "0009done\n"u8.ToArray();
        var remoteInput = new MemoryStream();
        var eofsSent = 0;

        await GitSessionProxyService.ForwardInputAsync(
            new MemoryStream(input),
            remoteInput,
            receivePackPrefix: null,
            sendEof: () => eofsSent++,
            CancellationToken.None
        );

        Assert.Equal(input, remoteInput.ToArray());
        Assert.Equal(0, eofsSent);
    }

    [Fact]
    public async Task ForwardInputAsync_WhenOnlyTheReceivePackPrefixIsSent_LeavesEofToTheStream()
    {
        var prefix = "0000"u8.ToArray();
        var remoteInput = new MemoryStream();
        var eofsSent = 0;

        await GitSessionProxyService.ForwardInputAsync(
            new MemoryStream(),
            remoteInput,
            prefix,
            sendEof: () => eofsSent++,
            CancellationToken.None
        );

        Assert.Equal(prefix, remoteInput.ToArray());
        Assert.Equal(0, eofsSent);
    }

    // Pins the SSH.NET internals SshCommandEof reaches by reflection: after an SSH.NET upgrade that
    // renames them, the type initializer throws and this test fails with TypeInitializationException.
    [Fact]
    public void SshCommandEof_WhenTheCommandIsNotExecuting_Throws()
    {
        var command = (SshCommand)RuntimeHelpers.GetUninitializedObject(typeof(SshCommand));

        Assert.Throws<InvalidOperationException>(() => SshCommandEof.Send(command));
    }
}
