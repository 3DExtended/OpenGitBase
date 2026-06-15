using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class GetFleetDispatcherSshPrivateKeyQuery
    : IQuery<string, GetFleetDispatcherSshPrivateKeyQuery>
{
    public string FleetBootstrapToken { get; set; } = string.Empty;
}
