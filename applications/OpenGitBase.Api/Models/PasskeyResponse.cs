namespace OpenGitBase.Api.Models;

public class PasskeyResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>The credential is synced, for example by iCloud Keychain or Google Password Manager.</summary>
    public bool IsBackedUp { get; set; }
}
