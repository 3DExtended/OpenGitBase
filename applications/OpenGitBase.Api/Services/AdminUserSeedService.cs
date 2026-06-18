using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Services;

public sealed class AdminUserSeedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AdminSeedOptions _options;
    private readonly ILogger<AdminUserSeedService> _logger;

    public AdminUserSeedService(
        IServiceScopeFactory scopeFactory,
        IOptions<AdminSeedOptions> options,
        ILogger<AdminUserSeedService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (environment.IsEnvironment("E2ETest"))
        {
            return;
        }

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        var emailProtection = scope.ServiceProvider.GetRequiredService<IEmailProtectionService>();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var adminUserId = await EnsureAdminUserAsync(
                context,
                passwordHasher,
                emailProtection,
                cancellationToken
            )
            .ConfigureAwait(false);
        await EnsureOrganizationAsync(context, adminUserId, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<Guid> EnsureAdminUserAsync(
        OpenGitBaseDbContext context,
        IPasswordHasherService passwordHasher,
        IEmailProtectionService emailProtection,
        CancellationToken cancellationToken
    )
    {
        var normalized = _options.Username.Trim().ToUpperInvariant();
        var existing = await context
            .Set<UserEntity>()
            .FirstOrDefaultAsync(user => user.NormalizedUsername == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            if (!existing.IsAdmin)
            {
                existing.IsAdmin = true;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Promoted existing user {Username} to admin.", _options.Username);
            }

            return existing.Id;
        }

        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        context.Set<UserEntity>().Add(
            new UserEntity
            {
                Id = userId,
                Username = _options.Username.Trim(),
                NormalizedUsername = normalized,
                CreatedAt = now,
                IsAdmin = true,
            }
        );
        context.Set<UserCredentialsEntity>().Add(
            new UserCredentialsEntity
            {
                UserId = userId,
                Username = _options.Username.Trim(),
                PasswordHash = passwordHasher.HashPassword(_options.Password),
                EmailCiphertext = emailProtection.EncryptEmail(_options.Email.Trim()),
                EmailLookupHash = emailProtection.ComputeLookupHash(_options.Email.Trim()),
                EmailVerified = true,
                Deleted = false,
                SignInProvider = false,
            }
        );
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Seeded admin user {Username}.", _options.Username);
        return userId;
    }

    private async Task EnsureOrganizationAsync(
        OpenGitBaseDbContext context,
        Guid adminUserId,
        CancellationToken cancellationToken
    )
    {
        var slug = _options.OrganizationSlug.Trim();
        var name = _options.OrganizationName.Trim();
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var existing = await context
            .Set<OrganizationEntity>()
            .FirstOrDefaultAsync(org => org.Slug == slug, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return;
        }

        var organizationId = Guid.NewGuid();
        context.Set<OrganizationEntity>().Add(
            new OrganizationEntity
            {
                Id = organizationId,
                Name = name,
                Slug = slug,
                OwnerUserId = adminUserId,
            }
        );
        context.Set<OrganizationMemberEntity>().Add(
            new OrganizationMemberEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = adminUserId,
                Role = OrganizationMemberRole.Owner,
            }
        );
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Seeded organization {OrganizationName} ({OrganizationSlug}) for admin user.",
            name,
            slug
        );
    }
}
