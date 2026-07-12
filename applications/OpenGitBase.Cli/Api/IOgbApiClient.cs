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
}
