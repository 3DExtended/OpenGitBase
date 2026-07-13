using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Output;

public sealed class JsonOutputWriter : IOutputWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly TextWriter _output;

    public JsonOutputWriter(TextWriter output) => _output = output;

    public void WriteAuthStatus(AuthStatusOutput status) => WriteObject(status);

    public void WriteIssueCreated(DiscussionModel discussion, string url) =>
        WriteObject(new
        {
            number = discussion.Number,
            title = discussion.Title,
            status = discussion.Status,
            url,
        });

    public void WriteIssueComment(DiscussionCommentModel comment, int issueNumber) =>
        WriteObject(new
        {
            issueNumber,
            commentId = comment.Id,
            author = comment.AuthorUsername,
            createdAt = comment.CreatedAt,
        });

    public void WriteIssueClosed(DiscussionModel discussion) =>
        WriteObject(new
        {
            number = discussion.Number,
            status = discussion.Status,
        });

    public void WriteIssueList(IReadOnlyList<DiscussionModel> discussions) =>
        WriteObject(new
        {
            issues = discussions.Select(d => new
            {
                number = d.Number,
                title = d.Title,
                status = d.Status,
                updatedAt = d.UpdatedAt,
            }),
        });

    public void WriteIssueView(DiscussionModel discussion, string url) =>
        WriteObject(new
        {
            number = discussion.Number,
            title = discussion.Title,
            status = discussion.Status,
            creator = discussion.CreatorUsername,
            assignee = discussion.AssigneeUsername,
            tags = discussion.Tags.Select(tag => tag.Name),
            url,
            createdAt = discussion.CreatedAt,
            updatedAt = discussion.UpdatedAt,
            comments = discussion.Comments?.Select(comment => new
            {
                id = comment.Id,
                author = comment.AuthorUsername,
                body = comment.BodyMarkdown,
                createdAt = comment.CreatedAt,
            }),
        });

    public void WriteIssueStatus(DiscussionStatus status) => WriteObject(new { status });

    public void WriteMergeRequestCreated(MergeRequestModel mergeRequest, string url) =>
        WriteObject(new
        {
            number = mergeRequest.Number,
            title = mergeRequest.Title,
            status = mergeRequest.Status,
            isDraft = mergeRequest.IsDraft,
            url,
        });

    public void WriteMergeRequestList(IReadOnlyList<MergeRequestModel> mergeRequests) =>
        WriteObject(new
        {
            mergeRequests = mergeRequests.Select(mr => new
            {
                number = mr.Number,
                title = mr.Title,
                status = mr.Status,
                isDraft = mr.IsDraft,
                sourceRef = mr.SourceRef,
                targetRef = mr.TargetRef,
                updatedAt = mr.UpdatedAt,
            }),
        });

    public void WriteMergeRequestView(
        MergeRequestModel mergeRequest,
        string url,
        IReadOnlyList<MergeRequestCommitModel>? commits) =>
        WriteObject(new
        {
            id = mergeRequest.Id,
            number = mergeRequest.Number,
            title = mergeRequest.Title,
            body = mergeRequest.Body,
            status = mergeRequest.Status,
            isDraft = mergeRequest.IsDraft,
            creator = mergeRequest.CreatorUsername,
            sourceRef = mergeRequest.SourceRef,
            targetRef = mergeRequest.TargetRef,
            sourceHeadSha = mergeRequest.SourceHeadSha,
            targetBaseSha = mergeRequest.TargetBaseSha,
            mergeCommitSha = mergeRequest.MergeCommitSha,
            approvalCountAtHead = mergeRequest.ApprovalCountAtHead,
            requiredApprovalCount = mergeRequest.RequiredApprovalCount,
            url,
            createdAt = mergeRequest.CreatedAt,
            updatedAt = mergeRequest.UpdatedAt,
            commits = commits?.Select(commit => new
            {
                sha = commit.Sha,
                shortSha = commit.ShortSha,
                message = commit.Message,
                authorName = commit.AuthorName,
                authoredAt = commit.AuthoredAt,
            }),
        });

    public void WriteMergeRequestStatus(
        MergeRequestModel mergeRequest,
        MergeRequestMergeabilityModel mergeability) =>
        WriteObject(new
        {
            status = mergeRequest.Status,
            isDraft = mergeRequest.IsDraft,
            approvalCountAtHead = mergeRequest.ApprovalCountAtHead,
            requiredApprovalCount = mergeRequest.RequiredApprovalCount,
            mergeability = new
            {
                status = mergeability.Status,
                message = mergeability.Message,
            },
        });

    public void WriteMergeRequestDiff(MergeRequestChangesModel changes) => WriteObject(changes);

    public void WriteMergeRequestClosed(MergeRequestModel mergeRequest) =>
        WriteObject(new { number = mergeRequest.Number, status = mergeRequest.Status });

    public void WriteMergeRequestReady(MergeRequestModel mergeRequest) =>
        WriteObject(new
        {
            number = mergeRequest.Number,
            status = mergeRequest.Status,
            isDraft = mergeRequest.IsDraft,
        });

    public void WriteMergeRequestApproved(MergeRequestModel mergeRequest) =>
        WriteObject(new
        {
            number = mergeRequest.Number,
            status = mergeRequest.Status,
            approvalCountAtHead = mergeRequest.ApprovalCountAtHead,
            requiredApprovalCount = mergeRequest.RequiredApprovalCount,
        });

    public void WriteMergeRequestEdited(MergeRequestModel mergeRequest) =>
        WriteObject(new
        {
            number = mergeRequest.Number,
            title = mergeRequest.Title,
            body = mergeRequest.Body,
        });

    public void WriteMergeRequestMerged(MergeRequestModel mergeRequest) =>
        WriteObject(new
        {
            number = mergeRequest.Number,
            status = mergeRequest.Status,
            mergeCommitSha = mergeRequest.MergeCommitSha,
        });

    public void WriteError(CliErrorOutput error) => WriteObject(error);

    private void WriteObject(object value)
    {
        _output.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
    }
}
