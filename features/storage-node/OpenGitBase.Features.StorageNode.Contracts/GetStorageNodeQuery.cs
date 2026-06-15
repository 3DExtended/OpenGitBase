using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.StorageNode.Contracts;

public class GetStorageNodeQuery
    : SingleModelQuery<StorageNodeDto, StorageNodeId, Guid, GetStorageNodeQuery>;
