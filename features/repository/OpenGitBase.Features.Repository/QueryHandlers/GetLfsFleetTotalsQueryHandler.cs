using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class GetLfsFleetTotalsQueryHandler : IQueryHandler<GetLfsFleetTotalsQuery, LfsFleetTotalsDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetLfsFleetTotalsQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<LfsFleetTotalsDto>> RunQueryAsync(
        GetLfsFleetTotalsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var objects = await context
            .Set<LfsObjectEntity>()
            .AsNoTracking()
            .GroupBy(entity => entity.RepositoryId)
            .Select(group => new { RepositoryId = group.Key, Count = group.Count(), Bytes = group.Sum(entity => entity.Size) })
            .ToListAsync(cancellationToken);
        var withLfs = context.Set<LfsObjectEntity>().Select(entity => entity.RepositoryId);
        var replicas = (await context
                .Set<RepositoryReplicaEntity>()
                .AsNoTracking()
                .Where(entity => withLfs.Contains(entity.RepositoryId))
                .Select(entity => new { entity.RepositoryId, entity.StorageNodeId, entity.Role })
                .ToListAsync(cancellationToken))
            .ToLookup(entity => entity.RepositoryId);
        var primaries = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(entity => withLfs.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.PrimaryStorageNodeId, cancellationToken);
        var copies = (await context
                .Set<LfsObjectCopyEntity>()
                .AsNoTracking()
                .GroupBy(entity => new { entity.RepositoryId, entity.StorageNodeId, entity.Kind })
                .Select(group => new { group.Key.RepositoryId, group.Key.StorageNodeId, group.Key.Kind, Count = group.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(copy => (copy.RepositoryId, copy.StorageNodeId, copy.Kind), copy => copy.Count);

        var missingCopies = 0;
        var repositoriesWithMissingCopies = 0;
        foreach (var repository in objects)
        {
            var nodes = LfsCopyExpectation.Nodes(
                replicas[repository.RepositoryId].Select(replica => (replica.StorageNodeId, replica.Role)),
                primaries.GetValueOrDefault(repository.RepositoryId)
            );
            var missing = nodes.Sum(node =>
                Math.Max(0, repository.Count - copies.GetValueOrDefault((repository.RepositoryId, node.Key, node.Value)))
            );
            missingCopies += missing;
            repositoriesWithMissingCopies += missing > 0 ? 1 : 0;
        }

        return Option.From(
            new LfsFleetTotalsDto
            {
                ObjectCount = objects.Sum(repository => repository.Count),
                TotalBytes = objects.Sum(repository => repository.Bytes),
                MissingCopies = missingCopies,
                RepositoriesWithMissingCopies = repositoriesWithMissingCopies,
            }
        );
    }
}
