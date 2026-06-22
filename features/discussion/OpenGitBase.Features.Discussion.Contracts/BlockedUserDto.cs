using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class BlockedUserDto
{
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);
    public string? Username { get; set; }
    public UserId BlockedByUserId { get; set; } = UserId.From(Guid.Empty);
    public DateTimeOffset BlockedAt { get; set; }
    public string? Reason { get; set; }
}
