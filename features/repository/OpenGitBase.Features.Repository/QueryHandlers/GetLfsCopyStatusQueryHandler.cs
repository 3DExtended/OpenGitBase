using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class GetLfsCopyStatusQueryHandler : IQueryHandler<GetLfsCopyStatusQuery, LfsCopyStatusDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetLfsCopyStatusQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<LfsCopyStatusDto>> RunQueryAsync(
        GetLfsCopyStatusQuery query,
        CancellationToken cancellationToken
    )
    {
        var repositoryId = query.RepositoryId.Value;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var repository = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(entity => entity.Id == repositoryId)
            .Select(entity => new { entity.PrimaryStorageNodeId })
            .FirstOrDefaultAsync(cancellationToken);
        if (repository is null)
        {
            return Option<LfsCopyStatusDto>.None;
        }

        var objects = await context
            .Set<LfsObjectEntity>()
            .AsNoTracking()
            .Where(entity => entity.RepositoryId == repositoryId)
            .Select(entity => entity.Size)
            .ToListAsync(cancellationToken);
        var copies = await context
            .Set<LfsObjectCopyEntity>()
            .AsNoTracking()
            .Where(entity => entity.RepositoryId == repositoryId)
            .GroupBy(entity => new { entity.StorageNodeId, entity.Kind })
            .Select(group => new { group.Key.StorageNodeId, group.Key.Kind, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var replicas = await context
            .Set<RepositoryReplicaEntity>()
            .AsNoTracking()
            .Where(entity => entity.RepositoryId == repositoryId)
            .Select(entity => new { entity.StorageNodeId, entity.Role })
            .ToListAsync(cancellationToken);

        // The nodes that should hold copies: every replica, and the primary even when it has no replica row.
        var nodes = replicas
            .GroupBy(replica => replica.StorageNodeId)
            .ToDictionary(
                group => group.Key,
                group => group.Any(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
                    ? LfsObjectCopyKind.Encrypted
                    : LfsObjectCopyKind.Plaintext
            );
        if (repository.PrimaryStorageNodeId is { } primary)
        {
            nodes.TryAdd(primary, LfsObjectCopyKind.Plaintext);
        }

        var status = nodes
            .Select(node =>
            {
                var held = copies
                    .Where(copy => copy.StorageNodeId == node.Key && copy.Kind == node.Value)
                    .Sum(copy => copy.Count);
                return new LfsNodeCopyStatusDto
                {
                    StorageNodeId = node.Key,
                    Kind = node.Value,
                    IsPrimary = node.Key == repository.PrimaryStorageNodeId,
                    Copies = held,
                    Missing = Math.Max(0, objects.Count - held),
                };
            })
            .OrderByDescending(node => node.IsPrimary)
            .ThenBy(node => node.Kind)
            .ThenBy(node => node.StorageNodeId)
            .ToList();
        return Option.From(
            new LfsCopyStatusDto
            {
                ObjectCount = objects.Count,
                TotalBytes = objects.Sum(),
                Nodes = status,
            }
        );
    }
}
