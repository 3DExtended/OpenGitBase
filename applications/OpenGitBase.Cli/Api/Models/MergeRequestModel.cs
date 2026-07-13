namespace OpenGitBase.Cli.Api.Models;

public sealed class MergeRequestModel
{
    public Guid Id { get; set; }

    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public MergeRequestStatus Status { get; set; }

    public bool IsDraft { get; set; }

    public string? CreatorUsername { get; set; }

    public string SourceRef { get; set; } = string.Empty;

    public string TargetRef { get; set; } = string.Empty;

    public string SourceHeadSha { get; set; } = string.Empty;

    public string TargetBaseSha { get; set; } = string.Empty;

    public string? MergeCommitSha { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int RequiredApprovalCount { get; set; }

    public int ApprovalCountAtHead { get; set; }
}
