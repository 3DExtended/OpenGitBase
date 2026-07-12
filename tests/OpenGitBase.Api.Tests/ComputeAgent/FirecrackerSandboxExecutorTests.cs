using NSubstitute;
using OpenGitBase.ComputeAgent;

namespace OpenGitBase.Api.Tests.ComputeAgent;

public class FirecrackerSandboxExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_UsesRootWhenEnvironmentSpecifies()
    {
        var launcher = Substitute.For<IFirecrackerLauncher>();
        launcher
            .LaunchAsync(Arg.Any<FirecrackerLaunchRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new FirecrackerLaunchResult
                {
                    Success = true,
                    ExitCode = 0,
                    DurationMs = 1,
                    StdOut = "ok",
                }
            );

        var executor = new FirecrackerSandboxExecutor(launcher);
        await executor.ExecuteAsync(
            "echo hi",
            "/tmp",
            new Dictionary<string, string> { ["OGB_SANDBOX_USER"] = "root" },
            CancellationToken.None
        );

        await launcher.Received(1)
            .LaunchAsync(
                Arg.Is<FirecrackerLaunchRequest>(request => request.RunAsUser == "root"),
                Arg.Any<CancellationToken>()
            );
    }
}
