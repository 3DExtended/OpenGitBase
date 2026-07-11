using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class IngestGitPushQueryHandler : IQueryHandler<IngestGitPushQuery, bool>
{
    private readonly IGitPushEventPublisher _publisher;

    public IngestGitPushQueryHandler(IGitPushEventPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task<Option<bool>> RunQueryAsync(
        IngestGitPushQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || string.IsNullOrWhiteSpace(query.AfterSha))
        {
            return Option<bool>.None;
        }

        await _publisher
            .PublishAsync(query.RepositoryId, query.Ref, query.AfterSha, cancellationToken)
            .ConfigureAwait(false);
        return Option.From(true);
    }
}
