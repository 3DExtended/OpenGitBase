using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class GenerateFleetDispatcherSshKeysQuery
    : IQuery<GenerateFleetDispatcherSshKeysResult, GenerateFleetDispatcherSshKeysQuery>;
