using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class GetOrganizationInviteByTokenQueryHandler
    : IQueryHandler<GetOrganizationInviteByTokenQuery, OrganizationInvitePublicDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _emailProtectionService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public GetOrganizationInviteByTokenQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService emailProtectionService,
        IPasswordHasherService passwordHasherService,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _emailProtectionService = emailProtectionService;
        _passwordHasherService = passwordHasherService;
        _systemClock = systemClock;
    }

    public async Task<Option<OrganizationInvitePublicDto>> RunQueryAsync(
        GetOrganizationInviteByTokenQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Token))
        {
            return Option<OrganizationInvitePublicDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context
            .Set<OrganizationInviteEntity>()
            .AsNoTracking()
            .Where(x => x.Status == OrganizationInviteStatus.Pending)
            .Join(
                context.Set<OrganizationEntity>().AsNoTracking(),
                invite => invite.OrganizationId,
                organization => organization.Id,
                (invite, organization) => new { Invite = invite, Organization = organization }
            )
            .ToListAsync(cancellationToken);

        var match = candidates.FirstOrDefault(x =>
            _passwordHasherService.VerifyPassword(x.Invite.TokenHash, query.Token)
        );
        if (match == null)
        {
            return Option<OrganizationInvitePublicDto>.None;
        }

        var utcNow = _systemClock.UtcNow;
        var email = _emailProtectionService.DecryptEmail(match.Invite.EmailCiphertext);
        return Option.From(
            new OrganizationInvitePublicDto
            {
                InviteId = OrganizationInviteId.From(match.Invite.Id),
                OrganizationId = OrganizationId.From(match.Organization.Id),
                OrganizationName = match.Organization.Name,
                OrganizationSlug = match.Organization.Slug,
                Email = OrganizationInviteTokenUtility.RedactEmail(email),
                Role = match.Invite.Role,
                ExpiresAt = match.Invite.ExpiresAt,
                Status = OrganizationInviteTokenUtility.ResolveStatus(match.Invite, utcNow),
            }
        );
    }
}
