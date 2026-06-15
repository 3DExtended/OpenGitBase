using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class ListPublicRepositoriesQuery : IQuery<IReadOnlyList<RepositoryDto>, ListPublicRepositoriesQuery>
{
    public string? Search { get; set; }
}
