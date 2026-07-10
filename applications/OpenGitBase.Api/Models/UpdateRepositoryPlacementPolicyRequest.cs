using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Api.Models;

public sealed class UpdateRepositoryPlacementPolicyRequest
{
    public PlacementPolicy? PlacementPolicy { get; init; }
}
