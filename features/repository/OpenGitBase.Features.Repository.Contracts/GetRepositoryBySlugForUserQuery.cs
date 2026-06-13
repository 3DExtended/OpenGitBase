using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.Contracts;

public class GetRepositoryBySlugForUserQuery
    : IQuery<RepositoryDto, GetRepositoryBySlugForUserQuery>
{
    public UserId OwnerUserId { get; set; } = default!;
    public string Slug { get; set; } = string.Empty;
}
