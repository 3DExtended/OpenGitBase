using OpenGitBase.Cli.Api.Models;
using OpenGitBase.Cli.Git;

namespace OpenGitBase.Cli.Api;

public interface IOgbApiClient
{
    Task<DiscussionModel> CreateDiscussionAsync(
        RepoSlug repo,
        CreateDiscussionRequest request,
        CancellationToken cancellationToken = default);

    Task<DiscussionCommentModel> CreateCommentAsync(
        RepoSlug repo,
        int number,
        CreateDiscussionCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<DiscussionModel> ResolveDiscussionAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default);

    Task<DiscussionModel> DismissDiscussionAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscussionModel>> ListDiscussionsAsync(
        RepoSlug repo,
        DiscussionStatus? status,
        CancellationToken cancellationToken = default);

    Task<DiscussionModel> GetDiscussionAsync(
        RepoSlug repo,
        int number,
        bool includeComments,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MergeRequestModel>> ListMergeRequestsAsync(
        RepoSlug repo,
        MergeRequestStatus? status,
        CancellationToken cancellationToken = default);

    Task<MergeRequestModel> GetMergeRequestAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default);

    Task<MergeRequestModel> CreateMergeRequestAsync(
        RepoSlug repo,
        CreateMergeRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<MergeRequestModel> UpdateMergeRequestAsync(
        RepoSlug repo,
        int number,
        UpdateMergeRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<MergeRequestModel> PublishMergeRequestAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default);

    Task<MergeRequestModel> CloseMergeRequestAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default);

    Task<MergeRequestModel> ApproveMergeRequestAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default);

    Task<MergeRequestModel> MergeMergeRequestAsync(
        RepoSlug repo,
        int number,
        MergeMergeRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<MergeRequestChangesModel> GetMergeRequestChangesAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MergeRequestCommitModel>> ListMergeRequestCommitsAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default);

    Task<MergeRequestMergeabilityModel> GetMergeRequestMergeabilityAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default);

    Task<MergeRequestBranchAheadSummaryModel> GetBranchAheadSummaryAsync(
        RepoSlug repo,
        string refName,
        CancellationToken cancellationToken = default);
}
