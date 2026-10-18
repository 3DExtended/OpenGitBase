namespace OpenGitBase.Cli.Tests.TestSupport;

public sealed class FakeLoopbackAuthServer : OpenGitBase.Cli.Auth.ILoopbackAuthServer
{
    public OpenGitBase.Cli.Auth.LoopbackAuthSession? Session { get; private set; }

    public string TokenToReturn { get; set; } = "fake-token";

    public TimeSpan? RequestedTimeout { get; private set; }

    public Task<OpenGitBase.Cli.Auth.LoopbackAuthSession> StartAsync(CancellationToken cancellationToken = default)
    {
        Session = new OpenGitBase.Cli.Auth.LoopbackAuthSession
        {
            Port = 54321,
            State = "test-state",
        };
        return Task.FromResult(Session);
    }

    public Task<string> WaitForTokenAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        RequestedTimeout = timeout;
        return Task.FromResult(TokenToReturn);
    }

    public Task StopAsync() => Task.CompletedTask;
}
