using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class IsRepositoryUserBlockedQuery : IQuery<bool, IsRepositoryUserBlockedQuery>
{
    public Guid RepositoryId { get; set; }
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);
}
