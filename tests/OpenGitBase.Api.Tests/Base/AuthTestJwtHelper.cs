#pragma warning disable S6781 // JWT secret keys should not be disclosed -- this is only used for testing purposes
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OpenGitBase.Api.Tests.Base;

internal static class AuthTestJwtHelper
{
    public const string TestAppleClientId = "com.example.app.test";
    public const string TestAppleKeyId = "apple-test-key";

    private static readonly RSA TestAppleSigningRsa = RSA.Create(2048);

    /// <summary>Public half of the key the test server trusts as Apple's JWKS.</summary>
    public static RsaSecurityKey TestAppleVerificationKey { get; } =
        new(TestAppleSigningRsa.ExportParameters(includePrivateParameters: false))
        {
            KeyId = TestAppleKeyId,
        };

    public static string CreateAppleIdentityToken(
        string subject,
        string email,
        string issuer = "https://appleid.apple.com",
        string? audience = null,
        DateTime? expiresUtc = null
    )
    {
        var expires = expiresUtc ?? DateTime.UtcNow.AddHours(1);
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience ?? TestAppleClientId,
            claims: [new Claim("sub", subject), new Claim("email", email)],
            notBefore: expires.AddHours(-2),
            expires: expires,
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(TestAppleSigningRsa) { KeyId = TestAppleKeyId },
                SecurityAlgorithms.RsaSha256
            )
        );

        return handler.WriteToken(token);
    }

    public static string CreateAppleIdentityTokenWithUntrustedKey(string subject, string email)
    {
        using var untrustedKey = RSA.Create(2048);
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            issuer: "https://appleid.apple.com",
            audience: TestAppleClientId,
            claims: [new Claim("sub", subject), new Claim("email", email)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(untrustedKey) { KeyId = TestAppleKeyId },
                SecurityAlgorithms.RsaSha256
            )
        );

        return handler.WriteToken(token);
    }

    public static string CreateGoogleIdentityToken(string subject, string email)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            claims: [new Claim("sub", subject), new Claim("email", email)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("auth-test-signing-key-32-bytes!!")
                ),
                SecurityAlgorithms.HmacSha256
            )
        );

        return handler.WriteToken(token);
    }
}
#pragma warning restore S6781 // JWT secret keys should not be disclosed
