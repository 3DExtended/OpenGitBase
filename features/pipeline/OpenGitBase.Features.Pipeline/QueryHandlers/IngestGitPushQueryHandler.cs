using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class IngestGitPushQueryHandler : IQueryHandler<IngestGitPushQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public IngestGitPushQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
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

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var exists = await context
            .Set<GitPushOutboxEntity>()
            .AnyAsync(
                entity =>
                    entity.RepositoryId == query.RepositoryId && entity.AfterSha == query.AfterSha,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!exists)
        {
            context.Set<GitPushOutboxEntity>()
                .Add(
                    new GitPushOutboxEntity
                    {
                        Id = Guid.NewGuid(),
                        RepositoryId = query.RepositoryId,
                        Ref = query.Ref ?? string.Empty,
                        AfterSha = query.AfterSha,
                        Status = GitPushOutboxStatus.Pending,
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Option.From(true);
    }
}
