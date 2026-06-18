using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Services;

public interface IOrganizationAccessService
{
    Task<OrganizationOwnerAccessCheck> CheckOwnerAccessAsync(
        OrganizationId organizationId,
        UserId userId,
        CancellationToken cancellationToken
    );

    Task<OrganizationMemberAccessCheck> CheckMemberAccessAsync(
        OrganizationId organizationId,
        UserId userId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<OrganizationDeleteBlocker>> GetDeleteBlockersAsync(
        OrganizationId organizationId,
        CancellationToken cancellationToken
    );

    Task<bool> WouldRemoveLastOwnerAsync(
        OrganizationId organizationId,
        UserId userIdToRemove,
        CancellationToken cancellationToken
    );
}
