using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class GetFleetDispatcherSshPublicKeyQuery
    : IQuery<string, GetFleetDispatcherSshPublicKeyQuery>;
