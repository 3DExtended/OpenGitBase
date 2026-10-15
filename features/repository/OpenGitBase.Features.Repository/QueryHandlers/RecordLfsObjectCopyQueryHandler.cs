using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class RecordLfsObjectCopyQueryHandler : IQueryHandler<RecordLfsObjectCopyQuery, RecordLfsObjectResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public RecordLfsObjectCopyQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<RecordLfsObjectResult>> RunQueryAsync(
        RecordLfsObjectCopyQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var recorded = await context
            .Set<LfsObjectEntity>()
            .AnyAsync(x => x.RepositoryId == query.RepositoryId.Value && x.Oid == query.Oid, cancellationToken);
        if (!recorded)
        {
            return Option<RecordLfsObjectResult>.None;
        }

        var copies = context.Set<LfsObjectCopyEntity>();
        var exists = await copies.AnyAsync(
            x => x.RepositoryId == query.RepositoryId.Value && x.Oid == query.Oid && x.StorageNodeId == query.StorageNodeId,
            cancellationToken
        );
        if (exists)
        {
            return Option.From(new RecordLfsObjectResult { Created = false });
        }

        copies.Add(
            new LfsObjectCopyEntity
            {
                RepositoryId = query.RepositoryId.Value,
                Oid = query.Oid,
                StorageNodeId = query.StorageNodeId,
                Kind = query.Kind,
                StoredAt = DateTimeOffset.UtcNow,
            }
        );
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another completion recorded the same copy first.
            return Option.From(new RecordLfsObjectResult { Created = false });
        }

        return Option.From(new RecordLfsObjectResult { Created = true });
    }
}
