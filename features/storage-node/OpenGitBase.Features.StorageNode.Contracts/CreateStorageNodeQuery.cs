using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.StorageNode.Contracts;

public class CreateStorageNodeQuery
    : CreateQuery<StorageNodeDto, StorageNodeId, Guid, CreateStorageNodeQuery>;
