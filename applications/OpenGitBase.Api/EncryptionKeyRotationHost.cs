using Microsoft.EntityFrameworkCore;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;

namespace OpenGitBase.Api;

/// <summary>
/// One-shot mode that re-encrypts every stored secret with the current <c>Encryption:DataKey</c>
/// and recomputes email lookup hashes with the current <c>Encryption:Pepper</c>, then exits.
/// Values written with an older key are read through <c>Encryption:PreviousDataKeys</c>.
/// </summary>
internal static class EncryptionKeyRotationHost
{
    public const string EnvironmentVariableName = "OPENGITBASE_ROTATE_ENCRYPTION_KEYS";

    public static bool IsEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(EnvironmentVariableName),
            "true",
            StringComparison.OrdinalIgnoreCase
        );
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var encryption = builder.Configuration.GetSection("Encryption").Get<EncryptionOptions>();
        if (encryption is null || string.IsNullOrWhiteSpace(encryption.DataKey))
        {
            Console.Error.WriteLine("Encryption key rotation skipped: Encryption:DataKey is not configured.");
            return 1;
        }

        await using var provider = OpenGitBase.Common.DependencyInjectionHelpers.BuildDatabaseServiceProvider(
            builder.Configuration,
            builder.Environment,
            FeatureRegistration.GetFeatureAssemblies()
        );
        var contextFactory = provider.GetService<IDbContextFactory<OpenGitBaseDbContext>>();
        if (contextFactory is null)
        {
            Console.Error.WriteLine("Encryption key rotation skipped: no Sql connection configured.");
            return 1;
        }

        try
        {
            var result = await EncryptionKeyRotation
                .RotateAsync(contextFactory, new EmailProtectionService(encryption), CancellationToken.None)
                .ConfigureAwait(false);
            Console.WriteLine(
                $"Encryption key rotation finished: {result.ReprotectedValues} values re-encrypted, "
                    + $"{result.RecomputedLookupHashes} lookup hashes recomputed."
            );
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Encryption key rotation failed, nothing was changed: {ex.GetType().Name}: {ex.Message}"
            );
            return 1;
        }
    }
}
