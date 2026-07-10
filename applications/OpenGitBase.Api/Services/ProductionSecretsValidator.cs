using Microsoft.AspNetCore.Hosting;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Services;

public static class ProductionSecretsValidator
{
    private const string DefaultJwtKey =
        "dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-!";

    private const string DefaultEncryptionPepper = "dev-pepper-change-me";

    private const string DefaultEncryptionDataKey =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private const string DefaultPlatformMergeToken = "ogb_platform_merge_dev_token_change_me";

    private const string DefaultAdminSeedPassword = "change-me-admin";

    public static void Validate(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var violations = new List<string>();

        var jwtKey = configuration["Jwt:Key"];
        if (IsDevJwtKey(jwtKey))
        {
            violations.Add("Jwt:Key");
        }

        var pepper = configuration["Encryption:Pepper"];
        if (string.IsNullOrWhiteSpace(pepper)
            || string.Equals(pepper, DefaultEncryptionPepper, StringComparison.Ordinal))
        {
            violations.Add("Encryption:Pepper");
        }

        var dataKey = configuration["Encryption:DataKey"];
        if (string.IsNullOrWhiteSpace(dataKey)
            || string.Equals(dataKey, DefaultEncryptionDataKey, StringComparison.Ordinal))
        {
            violations.Add("Encryption:DataKey");
        }

        var platformMergeToken = configuration["PlatformMergeIdentity:AccessToken"];
        if (IsDevPlatformMergeToken(platformMergeToken))
        {
            violations.Add("PlatformMergeIdentity:AccessToken");
        }

        var adminSeed = configuration.GetSection("AdminSeed").Get<AdminSeedOptions>() ?? new AdminSeedOptions();
        if (adminSeed.Enabled
            && string.Equals(adminSeed.Password, DefaultAdminSeedPassword, StringComparison.Ordinal))
        {
            violations.Add("AdminSeed:Password");
        }

        if (configuration.GetValue<bool>("Debug:Features:EmailVerification")
            || configuration.GetValue<bool>("Debug__Features__EmailVerification"))
        {
            violations.Add("Debug:Features:EmailVerification");
        }

        if (IsE2eCaptureEnabled(configuration))
        {
            violations.Add("E2E:CaptureEmail");
        }

        if (violations.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Production startup aborted: replace dev placeholder configuration values before serving traffic: "
            + string.Join(", ", violations)
            + ". See docs/deployment/production-secrets.md.");
    }

    internal static bool IsDevJwtKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return true;
        }

        if (key.StartsWith("dev-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(key, DefaultJwtKey, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(key, string.Concat(Enumerable.Repeat("dev-key", 32)), StringComparison.Ordinal);
    }

    internal static bool IsDevPlatformMergeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        if (string.Equals(token, DefaultPlatformMergeToken, StringComparison.Ordinal))
        {
            return true;
        }

        return token.Contains("_dev_", StringComparison.OrdinalIgnoreCase)
            || token.Contains("change_me", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsE2eCaptureEnabled(IConfiguration configuration) =>
        string.Equals(configuration["E2E:CaptureEmail"], "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(configuration["E2E__CaptureEmail"], "true", StringComparison.OrdinalIgnoreCase);
}
