using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class MarkPrimaryReplicatingQueryHandler
    : IQueryHandler<MarkPrimaryReplicatingQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public MarkPrimaryReplicatingQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<bool>> RunQueryAsync(
        MarkPrimaryReplicatingQuery query,
        CancellationToken cancellationToken
    )
    {
        var marked = await ReplicationClaim
            .MarkPrimaryReplicatingAsync(
                _contextFactory,
                query.RepositoryId.Value,
                query.StorageNodeId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);
        return Option.From(marked);
    }
}
