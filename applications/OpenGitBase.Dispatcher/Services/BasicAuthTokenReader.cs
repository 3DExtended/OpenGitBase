using System.Text;
using Microsoft.AspNetCore.Http;

namespace OpenGitBase.Dispatcher.Services;

public static class BasicAuthTokenReader
{
    public static bool TryReadAccessToken(HttpRequest request, out string accessToken)
    {
        accessToken = string.Empty;

        if (!request.Headers.TryGetValue("Authorization", out var authorization))
        {
            return false;
        }

        var header = authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(
                Convert.FromBase64String(header["Basic ".Length..].Trim())
            );
        }
        catch (FormatException)
        {
            return false;
        }

        var separatorIndex = decoded.IndexOf(':');
        if (separatorIndex < 0)
        {
            return false;
        }

        accessToken = decoded[(separatorIndex + 1)..];
        return !string.IsNullOrWhiteSpace(accessToken);
    }
}
