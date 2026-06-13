using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

public class GetRepositoryQuery
    : SingleModelQuery<RepositoryDto, RepositoryId, Guid, GetRepositoryQuery>;
