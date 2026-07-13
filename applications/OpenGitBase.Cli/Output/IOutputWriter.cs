using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Output;

public interface IOutputWriter
{
    void WriteAuthStatus(AuthStatusOutput status);

    void WriteIssueCreated(DiscussionModel discussion, string url);

    void WriteIssueComment(DiscussionCommentModel comment, int issueNumber);

    void WriteIssueClosed(DiscussionModel discussion);

    void WriteIssueList(IReadOnlyList<DiscussionModel> discussions);

    void WriteIssueView(DiscussionModel discussion, string url);

    void WriteIssueStatus(DiscussionStatus status);

    void WriteMergeRequestCreated(MergeRequestModel mergeRequest, string url);

    void WriteMergeRequestList(IReadOnlyList<MergeRequestModel> mergeRequests);

    void WriteMergeRequestView(
        MergeRequestModel mergeRequest,
        string url,
        IReadOnlyList<MergeRequestCommitModel>? commits);

    void WriteMergeRequestStatus(
        MergeRequestModel mergeRequest,
        MergeRequestMergeabilityModel mergeability);

    void WriteMergeRequestDiff(MergeRequestChangesModel changes);

    void WriteMergeRequestClosed(MergeRequestModel mergeRequest);

    void WriteMergeRequestReady(MergeRequestModel mergeRequest);

    void WriteMergeRequestApproved(MergeRequestModel mergeRequest);

    void WriteMergeRequestEdited(MergeRequestModel mergeRequest);

    void WriteMergeRequestMerged(MergeRequestModel mergeRequest);

    void WriteError(CliErrorOutput error);
}
