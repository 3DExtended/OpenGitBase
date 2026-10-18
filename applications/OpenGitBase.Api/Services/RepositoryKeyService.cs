using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;

using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

public sealed class RepositoryKeyService : IRepositoryKeyService
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IRepositoryKeyProtectionService _keyProtectionService;

    public RepositoryKeyService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IRepositoryKeyProtectionService keyProtectionService
    )
    {
        _contextFactory = contextFactory;
        _keyProtectionService = keyProtectionService;
    }

    public async Task<int> GenerateAndStoreKeyAsync(
        Guid repositoryId,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var existing = await context
            .Set<RepositoryKeyEntity>()
            .FirstOrDefaultAsync(key => key.RepositoryId == repositoryId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing.KeyVersion;
        }

        var keyId = Guid.NewGuid();
        var keyCiphertext = _keyProtectionService.ProtectKeyMaterial(RandomNumberGenerator.GetBytes(32));

        // Every attempt inserts the same id, so a replayed COMMIT that already landed stores the key once.
        return await context
            .InsertOnceAsync(
                async ct =>
                {
                    var entity = new RepositoryKeyEntity
                    {
                        Id = keyId,
                        RepositoryId = repositoryId,
                        KeyCiphertext = keyCiphertext,
                        KeyVersion = 1,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                    context.Set<RepositoryKeyEntity>().Add(entity);
                    await context.SaveChangesAsync(ct).ConfigureAwait(false);
                    return entity.KeyVersion;
                },
                ct => context.Set<RepositoryKeyEntity>().AnyAsync(key => key.Id == keyId, ct),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task<EphemeralRepositoryKey?> TryGetEphemeralKeyForPrimaryAsync(
        Guid repositoryId,
        Guid callerStorageNodeId,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var repository = await context
            .Set<RepositoryEntity>()
            .Include(entity => entity.Replicas)
            .FirstOrDefaultAsync(entity => entity.Id == repositoryId, cancellationToken)
            .ConfigureAwait(false);

        if (repository is null)
        {
            return null;
        }

        var isPrimary =
            repository.PrimaryStorageNodeId == callerStorageNodeId
            || repository.Replicas.Any(replica =>
                replica.StorageNodeId == callerStorageNodeId
                && replica.Role == RepositoryReplicaRole.Primary
            );

        if (!isPrimary)
        {
            return null;
        }

        var keyEntity = await context
            .Set<RepositoryKeyEntity>()
            .FirstOrDefaultAsync(key => key.RepositoryId == repositoryId, cancellationToken)
            .ConfigureAwait(false);

        if (keyEntity is null)
        {
            return null;
        }

        return new EphemeralRepositoryKey(
            _keyProtectionService.UnprotectKeyMaterial(keyEntity.KeyCiphertext),
            keyEntity.KeyVersion
        );
    }

    public async Task<EphemeralRepositoryKey?> TryGetRepositoryKeyAsync(
        Guid repositoryId,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var keyEntity = await context
            .Set<RepositoryKeyEntity>()
            .FirstOrDefaultAsync(key => key.RepositoryId == repositoryId, cancellationToken)
            .ConfigureAwait(false);

        if (keyEntity is null)
        {
            return null;
        }

        return new EphemeralRepositoryKey(
            _keyProtectionService.UnprotectKeyMaterial(keyEntity.KeyCiphertext),
            keyEntity.KeyVersion
        );
    }
}
