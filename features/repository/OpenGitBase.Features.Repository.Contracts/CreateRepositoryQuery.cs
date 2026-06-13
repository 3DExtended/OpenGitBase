using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

public class CreateRepositoryQuery
    : CreateQuery<RepositoryDto, RepositoryId, Guid, CreateRepositoryQuery>;
