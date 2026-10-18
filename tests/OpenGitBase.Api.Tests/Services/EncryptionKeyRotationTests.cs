using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class EncryptionKeyRotationTests
{
    private const string Email = "user@example.com";
    private const string NodeApiToken = "Node-Token-ABC+/==";
    private const string DispatcherPrivateKey =
        "-----BEGIN OPENSSH PRIVATE KEY-----\nAbCdEf\n-----END OPENSSH PRIVATE KEY-----";

    private const string TotpSecret = "JBSWY3DPEHPK3PXP";
    private const string PendingTotpSecret = "GEZDGNBVGY3TQOJQ";

    private static readonly string PreviousDataKey = Convert.ToBase64String(new byte[32]);

    private static readonly string CurrentDataKey = Convert.ToBase64String(
        Enumerable.Repeat((byte)7, 32).ToArray()
    );

    private static readonly byte[] RepositoryKeyMaterial = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public async Task RotateAsync_ReencryptsEveryProtectedValueAndRecomputesLookupHashes()
    {
        await using var connection = SqliteTestConnection.OpenInMemory();
        await using var provider = await CreateProviderAsync(connection);
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await SeedAsync(contextFactory, Protection(PreviousDataKey, "old-pepper"));

        var result = await EncryptionKeyRotation.RotateAsync(
            contextFactory,
            Protection(CurrentDataKey, "new-pepper", PreviousDataKey),
            CancellationToken.None
        );

        var currentOnly = Protection(CurrentDataKey, "new-pepper");
        await using var context = await contextFactory.CreateDbContextAsync();
        var credentials = await context.Set<UserCredentialsEntity>().SingleAsync();
        Assert.Equal(Email, currentOnly.DecryptEmail(credentials.EmailCiphertext!));
        Assert.Equal(currentOnly.ComputeLookupHash(Email), credentials.EmailLookupHash);
        var invite = await context.Set<OrganizationInviteEntity>().SingleAsync();
        Assert.Equal(Email, currentOnly.DecryptEmail(invite.EmailCiphertext));
        Assert.Equal(currentOnly.ComputeLookupHash(Email), invite.EmailLookupHash);
        var repositoryKey = await context.Set<RepositoryKeyEntity>().SingleAsync();
        Assert.Equal(
            RepositoryKeyMaterial,
            new RepositoryKeyProtectionService(currentOnly).UnprotectKeyMaterial(repositoryKey.KeyCiphertext)
        );
        var node = await context.Set<StorageNodeEntity>().SingleAsync();
        Assert.Equal(NodeApiToken, currentOnly.DecryptSecret(node.ApiTokenProtected));
        var fleetSecrets = await context.Set<FleetSecretsEntity>().SingleAsync();
        Assert.Equal(DispatcherPrivateKey, currentOnly.DecryptSecret(fleetSecrets.DispatcherSshPrivateKeyProtected));
        var authenticator = await context.Set<UserTotpAuthenticatorEntity>().SingleAsync();
        Assert.Equal(TotpSecret, currentOnly.DecryptSecret(authenticator.SecretProtected!));
        Assert.Equal(PendingTotpSecret, currentOnly.DecryptSecret(authenticator.PendingSecretProtected!));
        Assert.Equal(7, result.ReprotectedValues);
    }

    [Fact]
    public async Task RotateAsync_WhenAValueCannotBeDecrypted_ChangesNothing()
    {
        await using var connection = SqliteTestConnection.OpenInMemory();
        await using var provider = await CreateProviderAsync(connection);
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await SeedAsync(contextFactory, Protection(PreviousDataKey, "old-pepper"));
        var before = await SnapshotAsync(contextFactory);

        await Assert.ThrowsAnyAsync<CryptographicException>(() =>
            EncryptionKeyRotation.RotateAsync(
                contextFactory,
                Protection(CurrentDataKey, "new-pepper"),
                CancellationToken.None
            )
        );

        Assert.Equal(before, await SnapshotAsync(contextFactory));
    }

    private static EmailProtectionService Protection(string dataKey, string pepper, params string[] previousDataKeys) =>
        new(
            new EncryptionOptions
            {
                DataKey = dataKey,
                Pepper = pepper,
                PreviousDataKeys = [.. previousDataKeys],
            }
        );

    private static async Task<ServiceProvider> CreateProviderAsync(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [
                    typeof(UsersMapsterConfig).Assembly,
                    typeof(OrganizationMapsterConfig).Assembly,
                    typeof(RepositoryMapsterConfig).Assembly,
                    typeof(StorageNodeMapsterConfig).Assembly,
                    typeof(AccountSecurityMapsterConfig).Assembly,
                ]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        var provider = services.BuildServiceProvider();
        await using var context = await provider
            .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
            .CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        return provider;
    }

    private static async Task SeedAsync(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        EmailProtectionService protection
    )
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Set<UserEntity>().Add(
            new UserEntity
            {
                Id = userId,
                Username = "user",
                NormalizedUsername = "USER",
                CreatedAt = now,
            }
        );
        context.Set<UserCredentialsEntity>().Add(
            new UserCredentialsEntity
            {
                UserId = userId,
                Username = "user",
                EmailCiphertext = protection.EncryptEmail(Email),
                EmailLookupHash = protection.ComputeLookupHash(Email),
            }
        );
        context.Set<OrganizationEntity>().Add(
            new OrganizationEntity
            {
                Id = organizationId,
                Name = "Org",
                Slug = "org",
                OwnerUserId = userId,
            }
        );
        context.Set<OrganizationInviteEntity>().Add(
            new OrganizationInviteEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EmailCiphertext = protection.EncryptEmail(Email),
                EmailLookupHash = protection.ComputeLookupHash(Email),
                TokenHash = "token-hash",
                InvitedByUserId = userId,
                CreatedAt = now,
                ExpiresAt = now.AddDays(7),
            }
        );
        context.Set<RepositoryEntity>().Add(
            new RepositoryEntity
            {
                Id = repositoryId,
                Name = "repo",
                Slug = "repo",
                OwnerUserId = userId,
                PhysicalPath = "repo.git",
            }
        );
        context.Set<RepositoryKeyEntity>().Add(
            new RepositoryKeyEntity
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                KeyCiphertext = new RepositoryKeyProtectionService(protection).ProtectKeyMaterial(
                    RepositoryKeyMaterial
                ),
                CreatedAt = now,
            }
        );
        context.Set<StorageNodeEntity>().Add(
            new StorageNodeEntity
            {
                Id = Guid.NewGuid(),
                NodeId = "storage-1",
                ApiTokenProtected = protection.EncryptSecret(NodeApiToken),
                RegisteredAt = now,
            }
        );
        context.Set<FleetSecretsEntity>().Add(
            new FleetSecretsEntity
            {
                Id = Guid.NewGuid(),
                DispatcherSshPrivateKeyProtected = protection.EncryptSecret(DispatcherPrivateKey),
                UpdatedAt = now,
            }
        );
        context.Set<UserTotpAuthenticatorEntity>().Add(
            new UserTotpAuthenticatorEntity
            {
                UserId = userId,
                SecretProtected = protection.EncryptSecret(TotpSecret),
                PendingSecretProtected = protection.EncryptSecret(PendingTotpSecret),
                CreatedAt = now,
            }
        );
        await context.SaveChangesAsync();
    }

    private static async Task<string> SnapshotAsync(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var credentials = await context.Set<UserCredentialsEntity>().AsNoTracking().SingleAsync();
        var invite = await context.Set<OrganizationInviteEntity>().AsNoTracking().SingleAsync();
        var repositoryKey = await context.Set<RepositoryKeyEntity>().AsNoTracking().SingleAsync();
        var node = await context.Set<StorageNodeEntity>().AsNoTracking().SingleAsync();
        var fleetSecrets = await context.Set<FleetSecretsEntity>().AsNoTracking().SingleAsync();
        var authenticator = await context.Set<UserTotpAuthenticatorEntity>().AsNoTracking().SingleAsync();
        return string.Join(
            "|",
            credentials.EmailCiphertext,
            credentials.EmailLookupHash,
            invite.EmailCiphertext,
            invite.EmailLookupHash,
            repositoryKey.KeyCiphertext,
            node.ApiTokenProtected,
            fleetSecrets.DispatcherSshPrivateKeyProtected,
            authenticator.SecretProtected,
            authenticator.PendingSecretProtected
        );
    }
}
