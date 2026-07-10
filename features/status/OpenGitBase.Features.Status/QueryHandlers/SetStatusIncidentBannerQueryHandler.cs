using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class SetStatusIncidentBannerQueryHandler
    : IQueryHandler<SetStatusIncidentBannerQuery, PublicStatusIncidentDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public SetStatusIncidentBannerQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<PublicStatusIncidentDto>> RunQueryAsync(
        SetStatusIncidentBannerQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Message) || query.Message.Length > 500)
        {
            return Option<PublicStatusIncidentDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var active = await context
            .Set<StatusIncidentBannerEntity>()
            .Where(entity => entity.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var banner in active)
        {
            banner.IsActive = false;
        }

        var now = DateTimeOffset.UtcNow;
        var created = new StatusIncidentBannerEntity
        {
            Id = Guid.NewGuid(),
            Message = query.Message.Trim(),
            Severity = query.Severity,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = query.AdminUserId,
        };
        context.Set<StatusIncidentBannerEntity>().Add(created);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new PublicStatusIncidentDto
            {
                Message = created.Message,
                Severity = created.Severity,
                UpdatedAt = created.UpdatedAt,
            }
        );
    }
}
