using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class MergeRequestApprovalDto
{
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);

    public string? Username { get; set; }

    public string CommitSha { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
