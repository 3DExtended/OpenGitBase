using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.StorageNode.Contracts;

public class UpdateStorageNodeQuery
    : UpdateCommand<StorageNodeDto, StorageNodeId, Guid, UpdateStorageNodeQuery>;
