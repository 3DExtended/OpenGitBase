using System.Security.Cryptography;

namespace OpenGitBase.Features.Pipeline;

public static class JobIdentityTokens
{
    public static string Mint(Guid jobId)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return $"{jobId:D}:{secret}";
    }

    public static bool TryParseJobId(string token, out Guid jobId)
    {
        jobId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var separator = token.IndexOf(':');
        if (separator <= 0)
        {
            return false;
        }

        return Guid.TryParse(token[..separator], out jobId);
    }
}
