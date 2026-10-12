using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Services;

public sealed class AppleIdentityTokenValidator : IAppleIdentityTokenValidator
{
    public const string Issuer = "https://appleid.apple.com";
    private const string MetadataAddress = Issuer + "/.well-known/openid-configuration";

    private readonly AppleAuthOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public AppleIdentityTokenValidator(
        AppleAuthOptions options,
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager
    )
    {
        _options = options;
        _configurationManager = configurationManager;
    }

    public static IConfigurationManager<OpenIdConnectConfiguration> CreateAppleConfigurationManager() =>
        new ConfigurationManager<OpenIdConnectConfiguration>(
            MetadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true }
        );

    public async Task<AppleIdentity?> ValidateAsync(
        string identityToken,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(identityToken) || string.IsNullOrWhiteSpace(_options.ClientId))
        {
            return null;
        }

        var result = await ValidateWithCurrentKeysAsync(identityToken, cancellationToken)
            .ConfigureAwait(false);

        // Apple rotates its signing keys; refresh the JWKS once when the key id is unknown.
        if (result?.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            _configurationManager.RequestRefresh();
            result = await ValidateWithCurrentKeysAsync(identityToken, cancellationToken)
                .ConfigureAwait(false);
        }

        if (result is not { IsValid: true })
        {
            return null;
        }

        if (
            !result.Claims.TryGetValue("sub", out var subjectValue)
            || subjectValue is not string subject
            || string.IsNullOrWhiteSpace(subject)
        )
        {
            return null;
        }

        var email = result.Claims.TryGetValue("email", out var emailValue)
            ? emailValue as string ?? string.Empty
            : string.Empty;

        return new AppleIdentity(subject, email);
    }

    private async Task<TokenValidationResult?> ValidateWithCurrentKeysAsync(
        string identityToken,
        CancellationToken cancellationToken
    )
    {
        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await _configurationManager
                .GetConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Apple's key set is unavailable: fail closed instead of trusting an unverified token.
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = _options.ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
        };

        return await _tokenHandler
            .ValidateTokenAsync(identityToken, parameters)
            .ConfigureAwait(false);
    }
}
