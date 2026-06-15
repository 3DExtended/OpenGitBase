using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class ListStorageNodeEnrollmentsQueryHandler
    : IQueryHandler<ListStorageNodeEnrollmentsQuery, IReadOnlyList<StorageNodeEnrollmentDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListStorageNodeEnrollmentsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<StorageNodeEnrollmentDto>>> RunQueryAsync(
        ListStorageNodeEnrollmentsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context
            .Set<StorageNodeEnrollmentEntity>()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var enrollments = entities
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => new StorageNodeEnrollmentDto
            {
                Id = entity.Id,
                NodeId = entity.NodeId,
                CreatedAt = entity.CreatedAt,
                ExpiresAt = entity.ExpiresAt,
                ConsumedAt = entity.ConsumedAt,
            })
            .ToList();

        return Option.From<IReadOnlyList<StorageNodeEnrollmentDto>>(enrollments);
    }
}
