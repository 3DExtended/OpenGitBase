using System.Security.Cryptography;

namespace OpenGitBase.Features.ComputeNode;

public static class ComputeNodeIdentityTokens
{
    public static string Mint(Guid nodeId)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return $"{nodeId:D}:{secret}";
    }

    public static bool TryParseNodeId(string token, out Guid nodeId)
    {
        nodeId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var separator = token.IndexOf(':');
        if (separator <= 0)
        {
            return false;
        }

        return Guid.TryParse(token[..separator], out nodeId);
    }
}
