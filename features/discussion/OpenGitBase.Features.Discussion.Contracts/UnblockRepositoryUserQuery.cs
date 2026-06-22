using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class UnblockRepositoryUserQuery : IQuery<Unit, UnblockRepositoryUserQuery>
{
    public Guid RepositoryId { get; set; }
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);
}
