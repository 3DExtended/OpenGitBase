using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Models;

public sealed class CreateStorageNodeEnrollmentRequest
{
    public string NodeId { get; init; } = string.Empty;

    public int ExpiresInHours { get; init; } = 168;

    public long MaxBytes { get; init; }

    public HostingScope HostingScope { get; init; } = HostingScope.OwnOrgOnly;
}
