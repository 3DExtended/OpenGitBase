using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Services;

public class OrganizationAccessService : IOrganizationAccessService
{
    private readonly IQueryProcessor _queryProcessor;

    public OrganizationAccessService(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    public async Task<OrganizationOwnerAccessCheck> CheckOwnerAccessAsync(
        OrganizationId organizationId,
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        var organizationResult = await _queryProcessor
            .RunQueryAsync(new GetOrganizationQuery { ModelId = organizationId }, cancellationToken)
            .ConfigureAwait(false);

        if (organizationResult.IsNone)
        {
            return new OrganizationOwnerAccessCheck(false, false, null);
        }

        var organization = organizationResult.Get();
        var isOwner = await IsOwnerAsync(organization, userId, cancellationToken)
            .ConfigureAwait(false);
        return new OrganizationOwnerAccessCheck(true, isOwner, organization);
    }

    public async Task<OrganizationMemberAccessCheck> CheckMemberAccessAsync(
        OrganizationId organizationId,
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        var organizationResult = await _queryProcessor
            .RunQueryAsync(new GetOrganizationQuery { ModelId = organizationId }, cancellationToken)
            .ConfigureAwait(false);

        if (organizationResult.IsNone)
        {
            return new OrganizationMemberAccessCheck(false, false, false, null);
        }

        var organization = organizationResult.Get();
        var isOwner = await IsOwnerAsync(organization, userId, cancellationToken)
            .ConfigureAwait(false);

        if (isOwner)
        {
            return new OrganizationMemberAccessCheck(true, true, true, organization);
        }

        var membershipResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationMemberQuery
                {
                    OrganizationId = organization.Id,
                    UserId = userId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        var isMember = membershipResult.IsSome;
        return new OrganizationMemberAccessCheck(true, isMember, false, organization);
    }

    public async Task<IReadOnlyList<OrganizationDeleteBlocker>> GetDeleteBlockersAsync(
        OrganizationId organizationId,
        CancellationToken cancellationToken
    )
    {
        var repositoriesResult = await _queryProcessor
            .RunQueryAsync(
                new ListRepositoryQuery { OwnerUserId = UserId.From(organizationId.Value) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (repositoriesResult.IsNone)
        {
            return Array.Empty<OrganizationDeleteBlocker>();
        }

        return repositoriesResult
            .Get()
            .Select(repository => new OrganizationDeleteBlocker
            {
                Type = "repository",
                Name = repository.Name,
                Slug = repository.Slug,
            })
            .ToList();
    }

    public async Task<bool> WouldRemoveLastOwnerAsync(
        OrganizationId organizationId,
        UserId userIdToRemove,
        CancellationToken cancellationToken
    )
    {
        var membersResult = await _queryProcessor
            .RunQueryAsync(
                new ListOrganizationMembersQuery { OrganizationId = organizationId },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (membersResult.IsNone)
        {
            return false;
        }

        var memberToRemove = membersResult
            .Get()
            .FirstOrDefault(member => member.UserId == userIdToRemove);

        if (memberToRemove == null || memberToRemove.Role != OrganizationMemberRole.Owner)
        {
            return false;
        }

        return membersResult.Get().Count(member => member.Role == OrganizationMemberRole.Owner)
            == 1;
    }

    private async Task<bool> IsOwnerAsync(
        OrganizationDto organization,
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        if (organization.OwnerUserId == userId.Value)
        {
            return true;
        }

        var membershipResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationMemberQuery
                {
                    OrganizationId = organization.Id,
                    UserId = userId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return membershipResult.IsSome
            && membershipResult.Get().Role == OrganizationMemberRole.Owner;
    }
}
