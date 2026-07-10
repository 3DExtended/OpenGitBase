using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Models;

public sealed class UpdateStorageNodeHostingScopeRequest
{
    public HostingScope HostingScope { get; init; }
}
