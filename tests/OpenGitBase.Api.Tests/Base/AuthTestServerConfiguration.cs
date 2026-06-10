using System.Text;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;

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
