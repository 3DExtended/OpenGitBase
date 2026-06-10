using Microsoft.AspNetCore.Identity;

namespace OpenGitBase.Common.Services;

public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<string> _hasher = new();

    public string HashPassword(string password) => _hasher.HashPassword(string.Empty, password);

    public bool VerifyPassword(string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(string.Empty, hashedPassword, providedPassword)
        != PasswordVerificationResult.Failed;
}
