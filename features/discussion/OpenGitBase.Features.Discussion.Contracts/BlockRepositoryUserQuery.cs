using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class BlockRepositoryUserQuery : IQuery<BlockedUserDto, BlockRepositoryUserQuery>
{
    public Guid RepositoryId { get; set; }
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);
    public UserId BlockedByUserId { get; set; } = UserId.From(Guid.Empty);
    public string? Reason { get; set; }
}
