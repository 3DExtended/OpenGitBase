using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Api.Services;

public readonly record struct OrganizationOwnerAccessCheck(
    bool OrganizationExists,
    bool IsOwner,
    OrganizationDto? Organization
);
