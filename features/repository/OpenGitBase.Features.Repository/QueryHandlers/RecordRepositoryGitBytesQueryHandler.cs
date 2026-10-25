using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public sealed class RecordRepositoryGitBytesQueryHandler
    : IQueryHandler<RecordRepositoryGitBytesQuery, long>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public RecordRepositoryGitBytesQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<long>> RunQueryAsync(
        RecordRepositoryGitBytesQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.GitBytes < 0)
        {
            return Option<long>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // One statement, relative to the row as it is: the previous measurement is swapped for
        // the new one, and bytes others added meanwhile (Git LFS verify) stay in the counter.
        var updated = await context
            .Set<RepositoryEntity>()
            .Where(x => x.Id == query.RepositoryId.Value)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(
                            x => x.StorageBytesUsed,
                            x => x.StorageBytesUsed - x.GitBytesUsed + query.GitBytes
                        )
                        .SetProperty(x => x.GitBytesUsed, query.GitBytes),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (updated == 0)
        {
            return Option<long>.None;
        }

        var storageBytesUsed = await context
            .Set<RepositoryEntity>()
            .Where(x => x.Id == query.RepositoryId.Value)
            .Select(x => x.StorageBytesUsed)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return Option.From(storageBytesUsed);
    }
}
