using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace OpenGitBase.Cli.Auth;

public static class JwtTokenReader
{
    public static string? TryGetUsername(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            return jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
