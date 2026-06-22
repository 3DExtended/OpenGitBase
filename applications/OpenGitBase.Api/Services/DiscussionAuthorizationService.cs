#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Services;

public sealed class DiscussionAuthorizationService
{
    private readonly RepositoryContentAuthorizationService _contentAuthorization;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DiscussionAuthorizationService(
        RepositoryContentAuthorizationService contentAuthorization,
        IQueryProcessor queryProcessor,
        IHttpContextAccessor httpContextAccessor
    )
    {
        _contentAuthorization = contentAuthorization;
        _queryProcessor = queryProcessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<RepositoryContentAccessResult> AuthorizeReadAsync(
        string ownerSlug,
        string slug,
        CancellationToken cancellationToken
    ) => _contentAuthorization.AuthorizeReadAsync(ownerSlug, slug, cancellationToken);

    public async Task<DiscussionParticipationResult> AuthorizeParticipateAsync(
        string ownerSlug,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var read = await AuthorizeReadAsync(ownerSlug, slug, cancellationToken)
            .ConfigureAwait(false);

        if (read.Kind != RepositoryContentAccessResultKind.Allowed || read.Repository is null)
        {
            return DiscussionParticipationResult.FromRead(read);
        }

        var userId = TryGetAuthenticatedUserId();
        if (userId is null)
        {
            return DiscussionParticipationResult.SignInRequired(read.Repository);
        }

        if (
            await IsBlockedAsync(read.Repository.Id.Value, userId, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return DiscussionParticipationResult.Blocked(read.Repository);
        }

        var role = await GetEffectiveRoleAsync(read.Repository, userId, cancellationToken)
            .ConfigureAwait(false);

        if (role < RepositoryRole.Reader)
        {
            return DiscussionParticipationResult.Forbidden(read.Repository);
        }

        return DiscussionParticipationResult.Allowed(read.Repository, userId, role);
    }

    public async Task<RepositoryRole> GetEffectiveRoleAsync(
        RepositoryDto repository,
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        if (repository.OwnerUserId == userId)
        {
            return RepositoryRole.Owner;
        }

        if (
            string.Equals(repository.OwnerKind, "organization", StringComparison.OrdinalIgnoreCase)
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

            if (organizationResult.IsSome)
            {
                var organization = organizationResult.Get();
                if (organization.OwnerUserId == userId.Value)
                {
                    return RepositoryRole.Owner;
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

                if (membership.IsSome)
                {
                    return RepositoryRole.Reader;
                }
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

        return member.IsSome ? member.Get().Role : RepositoryRole.None;
    }

    public async Task<bool> IsBlockedAsync(
        Guid repositoryId,
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new IsRepositoryUserBlockedQuery
                {
                    RepositoryId = repositoryId,
                    UserId = userId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome && result.Get();
    }

    public UserId? TryGetAuthenticatedUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var identityProviderId = httpContext.User.FindFirst("identityproviderid")?.Value;
        if (
            string.IsNullOrWhiteSpace(identityProviderId)
            || !Guid.TryParse(identityProviderId, out var userGuid)
        )
        {
            return null;
        }

        return UserId.From(userGuid);
    }
}

public sealed class DiscussionParticipationResult
{
    public DiscussionParticipationResultKind Kind { get; init; }
    public RepositoryDto? Repository { get; init; }
    public UserId? UserId { get; init; }
    public RepositoryRole Role { get; init; }

    public static DiscussionParticipationResult FromRead(RepositoryContentAccessResult read) =>
        read.Kind switch
        {
            RepositoryContentAccessResultKind.NotFound => new()
            {
                Kind = DiscussionParticipationResultKind.NotFound,
            },
            RepositoryContentAccessResultKind.Forbidden => new()
            {
                Kind = DiscussionParticipationResultKind.Forbidden,
            },
            _ => new() { Kind = DiscussionParticipationResultKind.Forbidden },
        };

    public static DiscussionParticipationResult SignInRequired(RepositoryDto repository) =>
        new()
        {
            Kind = DiscussionParticipationResultKind.SignInRequired,
            Repository = repository,
        };

    public static DiscussionParticipationResult Blocked(RepositoryDto repository) =>
        new() { Kind = DiscussionParticipationResultKind.Blocked, Repository = repository };

    public static DiscussionParticipationResult Forbidden(RepositoryDto repository) =>
        new() { Kind = DiscussionParticipationResultKind.Forbidden, Repository = repository };

    public static DiscussionParticipationResult Allowed(
        RepositoryDto repository,
        UserId userId,
        RepositoryRole role
    ) =>
        new()
        {
            Kind = DiscussionParticipationResultKind.Allowed,
            Repository = repository,
            UserId = userId,
            Role = role,
        };
}
