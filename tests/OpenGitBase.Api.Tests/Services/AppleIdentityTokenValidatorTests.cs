using System.Security.Cryptography;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Tests.Services;

public class AppleIdentityTokenValidatorTests
{
    private static readonly AppleAuthOptions Options = new()
    {
        ClientId = AuthTestJwtHelper.TestAppleClientId,
    };

    [Fact]
    public async Task ValidateAsync_WithTokenSignedByTrustedKey_ReturnsVerifiedIdentity()
    {
        var validator = CreateValidator(AuthTestJwtHelper.TestAppleVerificationKey);
        var token = AuthTestJwtHelper.CreateAppleIdentityToken("apple-sub", "a@example.com");

        var identity = await validator.ValidateAsync(token);

        Assert.NotNull(identity);
        Assert.Equal("apple-sub", identity.Subject);
        Assert.Equal("a@example.com", identity.Email);
    }

    [Fact]
    public async Task ValidateAsync_WithTokenSignedByUntrustedKey_ReturnsNull()
    {
        var validator = CreateValidator(AuthTestJwtHelper.TestAppleVerificationKey);
        var token = AuthTestJwtHelper.CreateAppleIdentityTokenWithUntrustedKey("apple-sub", "a@example.com");

        Assert.Null(await validator.ValidateAsync(token));
    }

    [Fact]
    public async Task ValidateAsync_WithMalformedToken_ReturnsNull()
    {
        var validator = CreateValidator(AuthTestJwtHelper.TestAppleVerificationKey);

        Assert.Null(await validator.ValidateAsync("not-a-jwt"));
    }

    [Fact]
    public async Task ValidateAsync_WhenClientIdIsNotConfigured_ReturnsNull()
    {
        var validator = new AppleIdentityTokenValidator(
            new AppleAuthOptions { ClientId = string.Empty },
            StaticKeys(AuthTestJwtHelper.TestAppleVerificationKey)
        );
        var token = AuthTestJwtHelper.CreateAppleIdentityToken("apple-sub", "a@example.com", audience: string.Empty);

        Assert.Null(await validator.ValidateAsync(token));
    }

    [Fact]
    public async Task ValidateAsync_WhenAppleRotatedItsKey_RefreshesKeySetOnceAndAccepts()
    {
        using var staleRsa = RSA.Create(2048);
        var staleKey = new RsaSecurityKey(staleRsa.ExportParameters(false)) { KeyId = "stale-key" };
        var keys = new RotatingConfigurationManager(
            KeySet(staleKey),
            KeySet(AuthTestJwtHelper.TestAppleVerificationKey)
        );
        var validator = new AppleIdentityTokenValidator(Options, keys);
        var token = AuthTestJwtHelper.CreateAppleIdentityToken("apple-sub", "a@example.com");

        var identity = await validator.ValidateAsync(token);

        Assert.NotNull(identity);
        Assert.Equal(1, keys.RefreshRequests);
    }

    [Fact]
    public async Task ValidateAsync_WhenAppleKeySetIsUnavailable_ReturnsNull()
    {
        var validator = new AppleIdentityTokenValidator(Options, new UnavailableConfigurationManager());
        var token = AuthTestJwtHelper.CreateAppleIdentityToken("apple-sub", "a@example.com");

        Assert.Null(await validator.ValidateAsync(token));
    }

    private static AppleIdentityTokenValidator CreateValidator(SecurityKey trustedKey) =>
        new(Options, StaticKeys(trustedKey));

    private static StaticConfigurationManager<OpenIdConnectConfiguration> StaticKeys(SecurityKey key) =>
        new(KeySet(key));

    private static OpenIdConnectConfiguration KeySet(SecurityKey key)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = AppleIdentityTokenValidator.Issuer };
        configuration.SigningKeys.Add(key);
        return configuration;
    }

    private sealed class RotatingConfigurationManager(
        OpenIdConnectConfiguration initial,
        OpenIdConnectConfiguration afterRefresh
    ) : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private OpenIdConnectConfiguration _current = initial;

        public int RefreshRequests { get; private set; }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            Task.FromResult(_current);

        public void RequestRefresh()
        {
            RefreshRequests++;
            _current = afterRefresh;
        }
    }

    private sealed class UnavailableConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            throw new InvalidOperationException("IDX20803: Unable to obtain configuration.");

        public void RequestRefresh()
        {
        }
    }
}
