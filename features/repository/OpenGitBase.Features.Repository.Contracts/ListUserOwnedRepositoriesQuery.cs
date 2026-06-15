using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.Contracts;

public class ListUserOwnedRepositoriesQuery
    : IQuery<IReadOnlyList<RepositorySummaryDto>, ListUserOwnedRepositoriesQuery>
{
    public UserId UserId { get; set; } = default!;
}
