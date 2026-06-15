using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class DeleteRepositoryWithStorageQuery
    : IQuery<DeleteRepositoryWithStorageResult, DeleteRepositoryWithStorageQuery>
{
    public RepositoryId Id { get; set; } = default!;
}
