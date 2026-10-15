using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class RecordLfsObjectQueryHandler : IQueryHandler<RecordLfsObjectQuery, RecordLfsObjectResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public RecordLfsObjectQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<RecordLfsObjectResult>> RunQueryAsync(
        RecordLfsObjectQuery query,
        CancellationToken cancellationToken
    )
    {
        await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
            if (!await context.Set<RepositoryEntity>().AnyAsync(x => x.Id == query.RepositoryId.Value, cancellationToken))
            {
                return Option<RecordLfsObjectResult>.None;
            }

            if (await IsRecordedAsync(context, query, cancellationToken))
            {
                return Option.From(new RecordLfsObjectResult { Created = false });
            }

            context
                .Set<LfsObjectEntity>()
                .Add(
                    new LfsObjectEntity
                    {
                        RepositoryId = query.RepositoryId.Value,
                        Oid = query.Oid,
                        Size = query.Size,
                        KeyVersion = query.KeyVersion,
                        CreatedAt = DateTimeOffset.UtcNow,
                        CreatedByUserId = query.CreatedByUserId?.Value,
                    }
                );
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Option.From(new RecordLfsObjectResult { Created = true });
            }
            catch (DbUpdateException)
            {
                // A concurrent verify of the same oid won the insert; fall through and confirm that below.
            }
        }

        await using var checkContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        if (await IsRecordedAsync(checkContext, query, cancellationToken))
        {
            return Option.From(new RecordLfsObjectResult { Created = false });
        }

        throw new InvalidOperationException($"Recording LFS object {query.Oid} failed.");
    }

    private static Task<bool> IsRecordedAsync(
        OpenGitBaseDbContext context,
        RecordLfsObjectQuery query,
        CancellationToken cancellationToken
    ) =>
        context
            .Set<LfsObjectEntity>()
            .AnyAsync(
                entity => entity.RepositoryId == query.RepositoryId.Value && entity.Oid == query.Oid,
                cancellationToken
            );
}
