namespace OpenGitBase.Dispatcher.Models;

public sealed class GitRefUpdate
{
    public required string RefName { get; init; }

    public required string OldSha { get; init; }

    public required string NewSha { get; init; }

    public bool? IsForcePush { get; init; }
}
