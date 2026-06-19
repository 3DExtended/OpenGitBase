using System.Security.Cryptography;
using System.Text;

namespace OpenGitBase.Features.GitAccessToken.QueryHandlers;

public static class GitAccessTokenUtility
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(90);

    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var encoded = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return $"ogb_{encoded}";
    }

    public static string ComputeLookupHash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
