#pragma warning disable SA1402 // File may only contain a single type
﻿using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.MergeRequest.Entities;

public class MergeRequestApprovalEntity
{
    public Guid MergeRequestId { get; set; }

    public Guid UserId { get; set; }

    public string CommitSha { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public MergeRequestEntity MergeRequest { get; set; } = default!;
}

public class MergeRequestDiscussionLinkEntity
{
    public Guid MergeRequestId { get; set; }

    public Guid DiscussionId { get; set; }

    public int RelationshipType { get; set; }

    public MergeRequestEntity MergeRequest { get; set; } = default!;
}
