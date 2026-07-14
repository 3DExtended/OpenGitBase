using System.Text;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Tests.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Base;

internal static class AuthTestServerConfiguration
{
    public static void ConfigureDatabase(IServiceCollection services, SqliteConnection connection)
    {
        services.RemoveAll<DbContextOptions<OpenGitBaseDbContext>>();
        services.RemoveAll<IDbContextFactory<OpenGitBaseDbContext>>();

        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite(connection)
        );
    }

    public static void ConfigureGoogleValidator(IServiceCollection services)
    {
        services.RemoveAll<IGoogleIdentityTokenValidator>();
        services.AddSingleton<
            IGoogleIdentityTokenValidator,
            PermissiveGoogleIdentityTokenValidator
        >();
    }

    public static void ConfigureOptions(IServiceCollection services)
    {
        var jwtOptions = new JwtOptions
        {
            Issuer = "api",
            Audience = "api",
            Key = string.Join(string.Empty, Enumerable.Repeat("asdfasdf", 32)),
            NumberOfSecondsToExpire = 12000,
        };

        services.RemoveAll<JwtOptions>();
        services.AddSingleton(jwtOptions);
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (
                            string.IsNullOrEmpty(context.Token)
                            && context.Request.Cookies.TryGetValue(
                                AuthCookieOptions.CookieName,
                                out var cookieToken
                            )
                        )
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.Key!)
                    ),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            }
        );

        services.RemoveAll<AppleAuthOptions>();
        services.AddSingleton(
            new AppleAuthOptions { ClientId = AuthTestJwtHelper.TestAppleClientId }
        );

        services.RemoveAll<EncryptionOptions>();
        services.AddSingleton(
            new EncryptionOptions
            {
                DataKey = Convert.ToBase64String(new byte[32]),
                Pepper = "test-pepper",
            }
        );

        services.RemoveAll<SendGridOptions>();
        services.AddSingleton(
            new SendGridOptions
            {
                ApiKey = "test-key",
                FromEmailAddress = "test@example.com",
                FromSenderName = "Test",
                IsDisabled = true,
            }
        );

        services.RemoveAll<ISendGridEmailSender>();
        services.AddSingleton<ISendGridEmailSender, SendGridEmailSender>();

        services.RemoveAll<IStorageProvisionerClient>();
        services.AddSingleton<IStorageProvisionerClient, FakeStorageProvisionerClient>();

        services.RemoveAll<IStorageContentClient>();
        services.AddSingleton<IStorageContentClient, FakeStorageContentClient>();

        ConfigureDebugFeatures(services, emailVerificationEnabled: false);
    }

    public static void SeedDefaultStorageNode(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasher,
        IEmailProtectionService emailProtection
    )
    {
        using var context = contextFactory.CreateDbContext();
        if (context.Set<StorageNodeEntity>().Any())
        {
            return;
        }

        const string apiToken = "test-storage-token";
        var nodes = new[]
        {
            ("11111111-1111-1111-1111-111111111111", "test-storage-1", "storage-1"),
            ("22222222-2222-2222-2222-222222222222", "test-storage-2", "storage-2"),
            ("33333333-3333-3333-3333-333333333333", "test-storage-3", "storage-3"),
        };

        foreach (var (id, nodeId, host) in nodes)
        {
            context.Set<StorageNodeEntity>().Add(
                new StorageNodeEntity
                {
                    Id = Guid.Parse(id),
                    NodeId = nodeId,
                    InternalHost = host,
                    InternalSshPort = 22,
                    InternalHttpPort = 8081,
                    ApiTokenHash = passwordHasher.HashPassword(apiToken),
                    ApiTokenProtected = emailProtection.EncryptSecret(apiToken),
                    FreeBytesAvailable = 1_000_000_000,
                    TotalBytesAvailable = 2_000_000_000,
                    IsHealthy = true,
                    LastHeartbeatAt = DateTimeOffset.UtcNow,
                    RegisteredAt = DateTimeOffset.UtcNow,
                }
            );
        }

        context.SaveChanges();
    }

    public static void ConfigureDebugFeatures(
        IServiceCollection services,
        bool emailVerificationEnabled
    )
    {
        services.RemoveAll<DebugFeaturesOptions>();
        services.AddSingleton(
            new DebugFeaturesOptions
            {
                Features = new DebugFeatureFlags
                {
                    EmailVerification = emailVerificationEnabled,
                },
            }
        );
    }

    private sealed class PermissiveGoogleIdentityTokenValidator : IGoogleIdentityTokenValidator
    {
        public Task ValidateAsync(
            string identityToken,
            CancellationToken cancellationToken = default
        )
        {
            if (identityToken == "invalid-google-token")
            {
                throw new InvalidJwtException("Invalid token.");
            }

            return Task.CompletedTask;
        }
    }
}
