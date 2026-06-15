using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class CreateRepositoryWithStorageQuery
    : IQuery<CreateRepositoryWithStorageResult, CreateRepositoryWithStorageQuery>
{
    public RepositoryDto ModelToCreate { get; set; } = default!;
}
