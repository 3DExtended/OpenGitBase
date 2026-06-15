using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.StorageNode.Contracts;

public class DeleteStorageNodeQuery
    : DeleteCommand<StorageNodeDto, StorageNodeId, Guid, DeleteStorageNodeQuery>;
