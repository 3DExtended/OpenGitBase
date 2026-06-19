namespace OpenGitBase.Api.Models;

public sealed class GitConfigResponse
{
    public string GitBaseUrl { get; init; } = string.Empty;

    public bool SshEnabled { get; init; }
}
