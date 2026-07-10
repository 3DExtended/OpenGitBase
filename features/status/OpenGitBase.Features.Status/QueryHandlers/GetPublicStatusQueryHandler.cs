using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class GetPublicStatusQueryHandler
    : IQueryHandler<GetPublicStatusQuery, PublicStatusSnapshotDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetPublicStatusQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<PublicStatusSnapshotDto>> RunQueryAsync(
        GetPublicStatusQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context
            .Set<StatusSnapshotEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == StatusSnapshotEntity.SingletonId,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (entity is null || string.IsNullOrWhiteSpace(entity.PayloadJson))
        {
            return Option.From(
                new PublicStatusSnapshotDto
                {
                    OverallStatus = PublicHealthStatus.Unhealthy,
                    CheckedAt = DateTimeOffset.UtcNow,
                    Groups = [],
                    Incident = null,
                }
            );
        }

        var snapshot =
            JsonSerializer.Deserialize<PublicStatusSnapshotDto>(entity.PayloadJson, JsonOptions)
            ?? new PublicStatusSnapshotDto
            {
                OverallStatus = PublicHealthStatus.Unhealthy,
                CheckedAt = entity.CheckedAt,
            };

        return Option.From(snapshot);
    }
}
