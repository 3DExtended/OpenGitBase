using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class ListRecentPublicRepositoriesQuery
    : IQuery<IReadOnlyList<RepositoryDto>, ListRecentPublicRepositoriesQuery>
{
    public int Limit { get; set; } = 12;
}
