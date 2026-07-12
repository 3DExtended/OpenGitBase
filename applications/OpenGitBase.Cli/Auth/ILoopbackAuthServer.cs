namespace OpenGitBase.Cli.Auth;

public interface ILoopbackAuthServer
{
    Task<LoopbackAuthSession> StartAsync(CancellationToken cancellationToken = default);

    Task<string> WaitForTokenAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    Task StopAsync();
}
