using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Services;

public sealed class RepositoryContentAuthorizationService
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RepositoryContentAuthorizationService(
        IQueryProcessor queryProcessor,
        IHttpContextAccessor httpContextAccessor
    )
    {
        _queryProcessor = queryProcessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<RepositoryContentAccessResult> AuthorizeReadAsync(
        string ownerSlug,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var repositoryResult = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryByOwnerSlugQuery { OwnerSlug = ownerSlug, Slug = slug },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (repositoryResult.IsNone)
        {
            return RepositoryContentAccessResult.NotFound();
        }

        var repository = repositoryResult.Get();
        if (!repository.IsPrivate)
        {
            return RepositoryContentAccessResult.Allow(repository);
        }

        var userId = TryGetAuthenticatedUserId();
        if (userId is null)
        {
            return RepositoryContentAccessResult.NotFound();
        }

        if (await HasReadAccessAsync(repository, userId, cancellationToken).ConfigureAwait(false))
        {
            return RepositoryContentAccessResult.Allow(repository);
        }

        return RepositoryContentAccessResult.Forbidden();
    }

    private UserId? TryGetAuthenticatedUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var identityProviderId = httpContext.User.FindFirst("identityproviderid")?.Value;
        if (string.IsNullOrWhiteSpace(identityProviderId) || !Guid.TryParse(identityProviderId, out var userGuid))
        {
            return null;
        }

        return UserId.From(userGuid);
    }

    private async Task<bool> HasReadAccessAsync(
        RepositoryDto repository,
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        if (repository.OwnerUserId == userId)
        {
            return true;
        }

        if (
            string.Equals(repository.OwnerKind, "organization", StringComparison.OrdinalIgnoreCase)
        )
        {
            var orgAccess = await ResolveOrganizationReadAccessAsync(
                repository,
                userId,
                cancellationToken
            ).ConfigureAwait(false);
            if (orgAccess is not null)
            {
                return orgAccess.Value;
            }
        }

        var member = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryMemberQuery
                {
                    RepositoryId = repository.Id,
                    UserId = userId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return member.IsSome && member.Get().Role >= RepositoryRole.Reader;
    }

    private async Task<bool?> ResolveOrganizationReadAccessAsync(
        RepositoryDto repository,
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        var organizationResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationQuery
                {
                    ModelId = OrganizationId.From(repository.OwnerUserId.Value),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (organizationResult.IsNone)
        {
            return null;
        }

        var organization = organizationResult.Get();
        if (organization.OwnerUserId == userId.Value)
        {
            return true;
        }

        var membership = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationMemberQuery
                {
                    OrganizationId = organization.Id,
                    UserId = userId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (membership.IsNone)
        {
            return false;
        }

        return true;
    }
}
