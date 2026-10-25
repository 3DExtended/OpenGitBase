using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class FindLfsObjectsQueryHandler : IQueryHandler<FindLfsObjectsQuery, IReadOnlyList<LfsObjectDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public FindLfsObjectsQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<LfsObjectDto>>> RunQueryAsync(
        FindLfsObjectsQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.Oids.Count == 0)
        {
            return Option.From<IReadOnlyList<LfsObjectDto>>([]);
        }

        var oids = query.Oids.Distinct(StringComparer.Ordinal).ToList();
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var objects = await context
            .Set<LfsObjectEntity>()
            .AsNoTracking()
            .Where(entity => entity.RepositoryId == query.RepositoryId.Value && oids.Contains(entity.Oid))
            .Select(entity => new LfsObjectDto { Oid = entity.Oid, Size = entity.Size })
            .ToListAsync(cancellationToken);
        return Option.From<IReadOnlyList<LfsObjectDto>>(objects);
    }
}
