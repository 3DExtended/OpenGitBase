using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class ProductionSecretsValidatorTests
{
    [Fact]
    public void Validate_WhenDevelopment_DoesNotThrowForDevSecrets()
    {
        var environment = CreateEnvironment("Development");
        var configuration = CreateConfiguration(new Dictionary<string, string?>());

        var exception = Record.Exception(() => ProductionSecretsValidator.Validate(configuration, environment));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WhenProductionWithDefaultSecrets_Throws()
    {
        var environment = CreateEnvironment("Production");
        var configuration = CreateConfiguration(new Dictionary<string, string?>());

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionSecretsValidator.Validate(configuration, environment));

        Assert.Contains("Jwt:Key", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Encryption:Pepper", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PlatformMergeIdentity:AccessToken", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AdminSeed:Password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenProductionWithSafeSecrets_DoesNotThrow()
    {
        var environment = CreateEnvironment("Production");
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "prod-signing-key-32-bytes-minimum-length!!",
                ["Encryption:Pepper"] = "prod-pepper-secret-value",
                ["Encryption:DataKey"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
                ["PlatformMergeIdentity:AccessToken"] = "prod-platform-merge-token",
                ["AdminSeed:Enabled"] = "false",
                ["Debug:Features:EmailVerification"] = "false",
            });

        var exception = Record.Exception(() => ProductionSecretsValidator.Validate(configuration, environment));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-!")]
    [InlineData("dev-production-looking-key")]
    public void IsDevJwtKey_WhenDevPattern_ReturnsTrue(string? key)
    {
        Assert.True(ProductionSecretsValidator.IsDevJwtKey(key));
    }

    [Fact]
    public void IsDevJwtKey_WhenStartupFallbackKey_ReturnsTrue()
    {
        var fallbackKey = string.Concat(Enumerable.Repeat("dev-key", 32));

        Assert.True(ProductionSecretsValidator.IsDevJwtKey(fallbackKey));
    }

    [Fact]
    public void IsDevJwtKey_WhenProductionKey_ReturnsFalse()
    {
        Assert.False(ProductionSecretsValidator.IsDevJwtKey("prod-signing-key-32-bytes-minimum-length!!"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ogb_platform_merge_dev_token_change_me")]
    [InlineData("acme_dev_token")]
    public void IsDevPlatformMergeToken_WhenDevPattern_ReturnsTrue(string? token)
    {
        Assert.True(ProductionSecretsValidator.IsDevPlatformMergeToken(token));
    }

    [Fact]
    public void Validate_WhenProductionWithEmailVerificationBypass_Throws()
    {
        var environment = CreateEnvironment("Production");
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "prod-signing-key-32-bytes-minimum-length!!",
                ["Encryption:Pepper"] = "prod-pepper-secret-value",
                ["Encryption:DataKey"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
                ["PlatformMergeIdentity:AccessToken"] = "prod-platform-merge-token",
                ["AdminSeed:Enabled"] = "false",
                ["Debug:Features:EmailVerification"] = "true",
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionSecretsValidator.Validate(configuration, environment));

        Assert.Contains("Debug:Features:EmailVerification", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenProductionWithE2eCaptureEnabled_Throws()
    {
        var environment = CreateEnvironment("Production");
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "prod-signing-key-32-bytes-minimum-length!!",
                ["Encryption:Pepper"] = "prod-pepper-secret-value",
                ["Encryption:DataKey"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
                ["PlatformMergeIdentity:AccessToken"] = "prod-platform-merge-token",
                ["AdminSeed:Enabled"] = "false",
                ["E2E:CaptureEmail"] = "true",
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionSecretsValidator.Validate(configuration, environment));

        Assert.Contains("E2E:CaptureEmail", exception.Message, StringComparison.Ordinal);
    }

    private static IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-dev-!",
                    ["Encryption:Pepper"] = "dev-pepper-change-me",
                    ["Encryption:DataKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                    ["PlatformMergeIdentity:AccessToken"] = "ogb_platform_merge_dev_token_change_me",
                    ["AdminSeed:Enabled"] = "true",
                    ["AdminSeed:Password"] = "change-me-admin",
                    ["Debug:Features:EmailVerification"] = "false",
                }.Concat(values)
                    .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase))
            .Build();
    }
}
