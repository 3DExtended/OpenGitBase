using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class GenerateFleetDispatcherSshKeysQueryHandler
    : IQueryHandler<GenerateFleetDispatcherSshKeysQuery, GenerateFleetDispatcherSshKeysResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IEmailProtectionService _emailProtectionService;

    public GenerateFleetDispatcherSshKeysQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        IEmailProtectionService emailProtectionService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _emailProtectionService = emailProtectionService;
    }

    public async Task<Option<GenerateFleetDispatcherSshKeysResult>> RunQueryAsync(
        GenerateFleetDispatcherSshKeysQuery query,
        CancellationToken cancellationToken
    )
    {
        var keyPair = FleetSshKeyGenerator.GenerateEd25519KeyPair();
        var bootstrapToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context
            .Set<FleetSecretsEntity>()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            existing = new FleetSecretsEntity { Id = Guid.NewGuid() };
            context.Set<FleetSecretsEntity>().Add(existing);
        }

        existing.DispatcherSshPublicKey = keyPair.PublicKey;
        existing.DispatcherSshPrivateKeyProtected = _emailProtectionService.EncryptEmail(
            keyPair.PrivateKey
        );
        existing.FleetBootstrapTokenHash = _passwordHasherService.HashPassword(bootstrapToken);
        existing.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new GenerateFleetDispatcherSshKeysResult
            {
                DispatcherSshPublicKey = keyPair.PublicKey,
                FleetBootstrapToken = bootstrapToken,
            }
        );
    }
}
