namespace OpenGitBase.Dispatcher.Models;

public sealed class StorageRoutingTarget
{
    public string InternalHost { get; init; } = string.Empty;

    public int InternalSshPort { get; init; }

    public int InternalGitHttpPort { get; init; }

    public string Role { get; init; } = string.Empty;
}
