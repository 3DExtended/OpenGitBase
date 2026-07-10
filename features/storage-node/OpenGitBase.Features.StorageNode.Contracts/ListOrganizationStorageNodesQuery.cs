using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class ListOrganizationStorageNodesQuery
    : IQuery<IReadOnlyList<StorageNodeDto>, ListOrganizationStorageNodesQuery>
{
    public Guid OrganizationId { get; set; }
}
