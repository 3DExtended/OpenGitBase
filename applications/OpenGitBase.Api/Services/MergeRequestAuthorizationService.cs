#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Api.Models;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Services;

public enum MergeRequestAuthorizationResultKind
{
    NotFound,
    Forbidden,
    SignInRequired,
    Blocked,
    Allowed,
    InsufficientRole,
    SelfApprovalNotAllowed,
}

public sealed class MergeRequestAuthorizationResult
{
    public MergeRequestAuthorizationResultKind Kind { get; init; }
    public RepositoryDto? Repository { get; init; }
    public UserId? UserId { get; init; }
    public RepositoryRole Role { get; init; }

    public static MergeRequestAuthorizationResult FromParticipation(
        DiscussionParticipationResult participate
    ) =>
        participate.Kind switch
        {
            DiscussionParticipationResultKind.NotFound => new()
            {
                Kind = MergeRequestAuthorizationResultKind.NotFound,
            },
            DiscussionParticipationResultKind.Forbidden => new()
            {
                Kind = MergeRequestAuthorizationResultKind.Forbidden,
            },
            DiscussionParticipationResultKind.SignInRequired => new()
            {
                Kind = MergeRequestAuthorizationResultKind.SignInRequired,
                Repository = participate.Repository,
            },
            DiscussionParticipationResultKind.Blocked => new()
            {
                Kind = MergeRequestAuthorizationResultKind.Blocked,
                Repository = participate.Repository,
            },
            DiscussionParticipationResultKind.Allowed => new()
            {
                Kind = MergeRequestAuthorizationResultKind.Allowed,
                Repository = participate.Repository,
                UserId = participate.UserId,
                Role = participate.Role,
            },
            _ => new() { Kind = MergeRequestAuthorizationResultKind.Forbidden },
        };

    public static MergeRequestAuthorizationResult InsufficientRole(RepositoryDto repository) =>
        new()
        {
            Kind = MergeRequestAuthorizationResultKind.InsufficientRole,
            Repository = repository,
        };

    public static MergeRequestAuthorizationResult SelfApprovalNotAllowed(RepositoryDto repository) =>
        new()
        {
            Kind = MergeRequestAuthorizationResultKind.SelfApprovalNotAllowed,
            Repository = repository,
        };
}

public sealed class MergeRequestAuthorizationService
{
    private readonly DiscussionAuthorizationService _discussionAuthorization;

    public MergeRequestAuthorizationService(DiscussionAuthorizationService discussionAuthorization)
    {
        _discussionAuthorization = discussionAuthorization;
    }

    public Task<RepositoryContentAccessResult> AuthorizeReadAsync(
        string ownerSlug,
        string slug,
        CancellationToken cancellationToken
    ) => _discussionAuthorization.AuthorizeReadAsync(ownerSlug, slug, cancellationToken);

    public async Task<MergeRequestAuthorizationResult> AuthorizeParticipateAsync(
        string ownerSlug,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var participate = await _discussionAuthorization
            .AuthorizeParticipateAsync(ownerSlug, slug, cancellationToken)
            .ConfigureAwait(false);

        return MergeRequestAuthorizationResult.FromParticipation(participate);
    }

    public async Task<MergeRequestAuthorizationResult> AuthorizeCreateAsync(
        string ownerSlug,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var participate = await AuthorizeParticipateAsync(ownerSlug, slug, cancellationToken)
            .ConfigureAwait(false);

        if (participate.Kind != MergeRequestAuthorizationResultKind.Allowed)
        {
            return participate;
        }

        if (participate.Role < RepositoryRole.Writer)
        {
            return MergeRequestAuthorizationResult.InsufficientRole(participate.Repository!);
        }

        return participate;
    }

    public async Task<MergeRequestAuthorizationResult> AuthorizeApproveAsync(
        string ownerSlug,
        string slug,
        UserId mergeRequestAuthorUserId,
        CancellationToken cancellationToken
    )
    {
        var participate = await AuthorizeParticipateAsync(ownerSlug, slug, cancellationToken)
            .ConfigureAwait(false);

        if (participate.Kind != MergeRequestAuthorizationResultKind.Allowed)
        {
            return participate;
        }

        if (participate.Role < RepositoryRole.Writer)
        {
            return MergeRequestAuthorizationResult.InsufficientRole(participate.Repository!);
        }

        if (participate.UserId == mergeRequestAuthorUserId)
        {
            return MergeRequestAuthorizationResult.SelfApprovalNotAllowed(participate.Repository!);
        }

        return participate;
    }

    public async Task<MergeRequestAuthorizationResult> AuthorizeMergeAsync(
        string ownerSlug,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var participate = await AuthorizeParticipateAsync(ownerSlug, slug, cancellationToken)
            .ConfigureAwait(false);

        if (participate.Kind != MergeRequestAuthorizationResultKind.Allowed)
        {
            return participate;
        }

        if (participate.Role < RepositoryRole.Writer)
        {
            return MergeRequestAuthorizationResult.InsufficientRole(participate.Repository!);
        }

        return participate;
    }
}
