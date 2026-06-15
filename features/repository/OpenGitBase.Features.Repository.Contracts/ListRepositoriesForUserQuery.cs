using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.Contracts;

public class ListRepositoriesForUserQuery
    : IQuery<IReadOnlyList<RepositoryDto>, ListRepositoriesForUserQuery>
{
    public UserId UserId { get; set; } = default!;
}
