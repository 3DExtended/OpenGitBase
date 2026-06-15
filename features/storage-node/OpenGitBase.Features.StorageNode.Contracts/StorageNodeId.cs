using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.StorageNode.Contracts;

public record StorageNodeId : Identifier<Guid, StorageNodeId>;
