using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class GetRepositoryByOwnerSlugQuery : IQuery<RepositoryDto, GetRepositoryByOwnerSlugQuery>
{
    public string OwnerSlug { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
}
