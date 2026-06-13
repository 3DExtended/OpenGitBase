namespace OpenGitBase.Common.Services;

public class SshKeyService : ISshKeyService
{
    public string? ValidateAndGetFingerprint(string? publicSshKey)
    {
        if (string.IsNullOrWhiteSpace(publicSshKey))
        {
            throw new ArgumentException("Public key is empty", nameof(publicSshKey));
        }

        // Basic validation: Check if the key is in the expected format (e.g., starts with "ssh-rsa" or "ssh-ed25519")
        if (!publicSshKey.StartsWith("ssh-rsa ") && !publicSshKey.StartsWith("ssh-ed25519 "))
        {
            throw new ArgumentException(
                "Invalid SSH key format. Key must start with 'ssh-rsa' or 'ssh-ed25519'.",
                nameof(publicSshKey)
            );
        }

        try
        {
            // Extract the key part (the base64-encoded portion)
            var parts = publicSshKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                throw new ArgumentException("Invalid SSH key format.", nameof(publicSshKey));
            }

            // var keyType = parts[0]; // e.g. ssh-ed25519, ssh-rsa
            var keyBase64 = parts[1]; // AAAAC3NzaC1lZDI1...

            var keyBytes = Convert.FromBase64String(keyBase64);

            // Compute the fingerprint (using SHA256 hash of the key bytes)
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(keyBytes);
            var fingerprint = Convert.ToBase64String(hashBytes);

            return fingerprint;
        }
        catch
        {
            // If any exception occurs during parsing or hashing, consider the key invalid
            throw new ArgumentException("Invalid SSH key format.", nameof(publicSshKey));
        }
    }
}
