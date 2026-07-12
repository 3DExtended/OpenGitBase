namespace OpenGitBase.Cli.Auth;

public sealed class LoopbackAuthSession
{
    public required int Port { get; init; }

    public required string State { get; init; }

    public string CallbackPath { get; init; } = "/callback";
}
