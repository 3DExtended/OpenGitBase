using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Moves every value protected by <c>Encryption:DataKey</c> to the current key and recomputes the
/// email lookup hashes with the current <c>Encryption:Pepper</c>. Values written with an earlier key
/// are read through <c>Encryption:PreviousDataKeys</c>. All rows are saved in one transaction, so a
/// value that cannot be decrypted aborts the run without changing anything.
/// </summary>
public static class EncryptionKeyRotation
{
    public static async Task<EncryptionKeyRotationResult> RotateAsync(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService protection,
        CancellationToken cancellationToken
    )
    {
        await using var context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var reprotected = 0;
        var rehashed = 0;

        foreach (var credentials in await context
                     .Set<UserCredentialsEntity>()
                     .Where(entity => entity.EmailCiphertext != null && entity.EmailCiphertext != string.Empty)
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            credentials.EmailCiphertext = protection.Reprotect(credentials.EmailCiphertext!);
            reprotected++;
            if (!string.IsNullOrEmpty(credentials.EmailLookupHash))
            {
                credentials.EmailLookupHash = protection.ComputeLookupHash(
                    protection.DecryptEmail(credentials.EmailCiphertext)
                );
                rehashed++;
            }
        }

        foreach (var invite in await context
                     .Set<OrganizationInviteEntity>()
                     .Where(entity => entity.EmailCiphertext != string.Empty)
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            invite.EmailCiphertext = protection.Reprotect(invite.EmailCiphertext);
            invite.EmailLookupHash = protection.ComputeLookupHash(protection.DecryptEmail(invite.EmailCiphertext));
            reprotected++;
            rehashed++;
        }

        foreach (var repositoryKey in await context
                     .Set<RepositoryKeyEntity>()
                     .Where(entity => entity.KeyCiphertext != string.Empty)
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            repositoryKey.KeyCiphertext = protection.Reprotect(repositoryKey.KeyCiphertext);
            reprotected++;
        }

        foreach (var node in await context
                     .Set<StorageNodeEntity>()
                     .Where(entity => entity.ApiTokenProtected != string.Empty)
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            node.ApiTokenProtected = protection.Reprotect(node.ApiTokenProtected);
            reprotected++;
        }

        foreach (var fleetSecrets in await context
                     .Set<FleetSecretsEntity>()
                     .Where(entity => entity.DispatcherSshPrivateKeyProtected != string.Empty)
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            fleetSecrets.DispatcherSshPrivateKeyProtected = protection.Reprotect(
                fleetSecrets.DispatcherSshPrivateKeyProtected
            );
            reprotected++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new EncryptionKeyRotationResult(reprotected, rehashed);
    }
}
