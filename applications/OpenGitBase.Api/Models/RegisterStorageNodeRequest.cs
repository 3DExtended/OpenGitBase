namespace OpenGitBase.Api.Models;

public sealed class RegisterStorageNodeRequest
{
    public string NodeId { get; init; } = string.Empty;

    public string InternalHost { get; init; } = string.Empty;

    public int InternalSshPort { get; init; } = 22;

    public int InternalHttpPort { get; init; }

    public long FreeBytesAvailable { get; init; }

    public long TotalBytesAvailable { get; init; }
}
