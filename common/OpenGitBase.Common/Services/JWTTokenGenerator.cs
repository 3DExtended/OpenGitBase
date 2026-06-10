using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Common.Services;

#pragma warning disable S101 // Types should be named in PascalCase
public class JWTTokenGenerator : IJWTTokenGenerator
#pragma warning restore S101 // Types should be named in PascalCase
{
    private readonly JwtOptions _jwtOptions;
    private readonly ISystemClock _systemClock;

    public JWTTokenGenerator(ISystemClock systemClock, JwtOptions jwtOptions)
    {
        _systemClock = systemClock;
        _jwtOptions = jwtOptions;
    }

    public string GetJWTToken(string username, string userIdentityProviderId)
    {
        var issuer = _jwtOptions.Issuer ?? "api";
        var audience = _jwtOptions.Audience ?? "api";
        var key = Encoding.UTF8.GetBytes(
            _jwtOptions.Key ?? throw new InvalidOperationException("Jwt:Key is required.")
        );

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim("identityproviderid", userIdentityProviderId),
                new Claim(JwtRegisteredClaimNames.Name, username),
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            Expires = _systemClock.UtcNow.UtcDateTime.AddSeconds(
                _jwtOptions.NumberOfSecondsToExpire
            ),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha512Signature
            ),
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}
