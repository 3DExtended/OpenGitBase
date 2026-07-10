using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class UpdateRepositoryMaxBytesOverrideQuery
    : IQuery<RepositoryDto, UpdateRepositoryMaxBytesOverrideQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public long? MaxBytesOverride { get; set; }
}
