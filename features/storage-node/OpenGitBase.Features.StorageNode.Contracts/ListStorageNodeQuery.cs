using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.StorageNode.Contracts;

public class ListStorageNodeQuery
    : ListOfModelQuery<StorageNodeDto, StorageNodeId, Guid, ListStorageNodeQuery>;
