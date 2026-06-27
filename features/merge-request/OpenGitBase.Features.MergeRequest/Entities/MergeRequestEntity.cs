#pragma warning disable SA1402 // File may only contain a single type
﻿using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.MergeRequest.Entities;

public class MergeRequestEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public int Status { get; set; }
    public bool IsDraft { get; set; }
    public string SourceRef { get; set; } = string.Empty;
    public string TargetRef { get; set; } = string.Empty;
    public string SourceHeadSha { get; set; } = string.Empty;
    public string TargetBaseSha { get; set; } = string.Empty;
    public string? MergeCommitSha { get; set; }
    public Guid CreatorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
