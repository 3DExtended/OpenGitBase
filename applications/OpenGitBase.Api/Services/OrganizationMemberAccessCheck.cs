using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Api.Services;

public readonly record struct OrganizationMemberAccessCheck(
    bool OrganizationExists,
    bool IsMember,
    bool IsOwner,
    OrganizationDto? Organization
);
