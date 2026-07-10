using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class GetAdminStatusIncidentBannerQueryHandler
    : IQueryHandler<GetAdminStatusIncidentBannerQuery, PublicStatusIncidentDto?>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetAdminStatusIncidentBannerQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<PublicStatusIncidentDto?>> RunQueryAsync(
        GetAdminStatusIncidentBannerQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var banner = await context
            .Set<StatusIncidentBannerEntity>()
            .AsNoTracking()
            .Where(entity => entity.IsActive)
            .OrderByDescending(entity => entity.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (banner is null)
        {
            return Option.From<PublicStatusIncidentDto?>(null);
        }

        return Option.From<PublicStatusIncidentDto?>(
            new PublicStatusIncidentDto
            {
                Message = banner.Message,
                Severity = banner.Severity,
                UpdatedAt = banner.UpdatedAt,
            }
        );
    }
}
