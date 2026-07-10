using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed record ReplicaSetPlannerRequest(
    IReadOnlyList<StorageNodeDto> HealthyNodes,
    Guid? OwnerOrganizationId = null,
    PlacementPolicy PlacementPolicy = PlacementPolicy.Inherit,
    SelfHostPreference SelfHostPreference = SelfHostPreference.PlatformOnly,
    long RequiredBytesPerNode = 0,
    IReadOnlyDictionary<Guid, int>? RepositoryCountsByNodeId = null
);
