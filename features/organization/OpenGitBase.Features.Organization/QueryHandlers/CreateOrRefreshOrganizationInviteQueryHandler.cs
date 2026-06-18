using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class CreateOrRefreshOrganizationInviteQueryHandler
    : IQueryHandler<CreateOrRefreshOrganizationInviteQuery, CreateOrRefreshOrganizationInviteResult>
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _emailProtectionService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public CreateOrRefreshOrganizationInviteQueryHandler(
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

    public async Task<Option<CreateOrRefreshOrganizationInviteResult>> RunQueryAsync(
        CreateOrRefreshOrganizationInviteQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Email))
        {
            return Option<CreateOrRefreshOrganizationInviteResult>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var utcNow = _systemClock.UtcNow;
        var normalizedEmail = query.Email.Trim();
        var emailLookupHash = _emailProtectionService.ComputeLookupHash(normalizedEmail);
        var token = OrganizationInviteTokenUtility.GenerateToken();
        var tokenHash = _passwordHasherService.HashPassword(token);

        var existing = await context
            .Set<OrganizationInviteEntity>()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId == query.OrganizationId.Value
                    && x.EmailLookupHash == emailLookupHash
                    && x.Status == OrganizationInviteStatus.Pending,
                cancellationToken
            );

        if (existing != null)
        {
            existing.Role = query.Role;
            existing.TokenHash = tokenHash;
            existing.ExpiresAt = utcNow.Add(InviteLifetime);
            existing.InvitedByUserId = query.InvitedByUserId;
            await context.SaveChangesAsync(cancellationToken);
            return Option.From(
                new CreateOrRefreshOrganizationInviteResult
                {
                    InviteId = OrganizationInviteId.From(existing.Id),
                    Token = token,
                }
            );
        }

        var created = new OrganizationInviteEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = query.OrganizationId.Value,
            EmailLookupHash = emailLookupHash,
            EmailCiphertext = _emailProtectionService.EncryptEmail(normalizedEmail),
            Role = query.Role,
            TokenHash = tokenHash,
            InvitedByUserId = query.InvitedByUserId,
            CreatedAt = utcNow,
            ExpiresAt = utcNow.Add(InviteLifetime),
            Status = OrganizationInviteStatus.Pending,
        };
        context.Set<OrganizationInviteEntity>().Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return Option.From(
            new CreateOrRefreshOrganizationInviteResult
            {
                InviteId = OrganizationInviteId.From(created.Id),
                Token = token,
            }
        );
    }
}
