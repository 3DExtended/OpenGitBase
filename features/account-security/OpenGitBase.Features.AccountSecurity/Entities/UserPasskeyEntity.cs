using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.Entities;

/// <summary>
/// A registered WebAuthn credential. The public key is not secret, so it is stored as is and needs no
/// entry in the encryption key rotation.
/// </summary>
public class UserPasskeyEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    public byte[] CredentialId { get; set; } = [];

    /// <summary>The COSE-encoded public key.</summary>
    public byte[] PublicKey { get; set; } = [];

    /// <summary>The authenticator's signature counter; <c>long</c> because Npgsql maps <c>uint</c> to <c>xid</c>.</summary>
    public long SignCount { get; set; }

    public Guid Aaguid { get; set; }

    /// <summary>Comma-separated transport names, such as <c>Internal,Hybrid</c>.</summary>
    public string Transports { get; set; } = string.Empty;

    public bool IsBackupEligible { get; set; }

    public bool IsBackedUp { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}
