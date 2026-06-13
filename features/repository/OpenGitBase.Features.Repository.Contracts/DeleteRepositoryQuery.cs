using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

public class DeleteRepositoryQuery
    : DeleteCommand<RepositoryDto, RepositoryId, Guid, DeleteRepositoryQuery>;
