namespace OpenGitBase.Cli.Output;

public sealed class AuthStatusOutput
{
    public required bool LoggedIn { get; init; }

    public string? Hostname { get; init; }

    public string? Username { get; init; }
}
