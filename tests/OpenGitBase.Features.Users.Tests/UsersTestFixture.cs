using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.Tests;

internal static class UsersTestFixture
{
    internal static EncryptionOptions DefaultEncryptionOptions =>
        new() { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "test-pepper" };

    internal static async Task<(ServiceProvider Provider, SqliteConnection Connection)> CreateAsync(
        Action<ServiceCollection>? configure = null,
        DateTimeOffset? utcNow = null
    )
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(UsersMapsterConfig).Assembly])
        );
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite(connection)
        );
        services.AddLogging();

        var mapsterConfig = new TypeAdapterConfig();
        new UsersMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));

        services.AddSingleton(DefaultEncryptionOptions);
        services.AddSingleton<IEmailProtectionService, EmailProtectionService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ISystemClock>(new FixedSystemClock(utcNow ?? DateTimeOffset.UtcNow));

        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(UsersMapsterConfig).Assembly)
        );

        configure?.Invoke(services);

        var provider = services.BuildServiceProvider();
        await EnsureDatabaseAsync(provider);

        return (provider, connection);
    }

    internal static async Task EnsureDatabaseAsync(IServiceProvider provider)
    {
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    internal static async Task<Guid> SeedUserAsync(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        string username,
        DateTimeOffset? createdAt = null
    )
    {
        var userId = Guid.NewGuid();
        await using var context = await contextFactory.CreateDbContextAsync();
        context
            .Set<UserEntity>()
            .Add(
                new UserEntity
                {
                    Id = userId,
                    Username = username,
                    NormalizedUsername = username.Trim().ToLowerInvariant(),
                    CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
                }
            );
        await context.SaveChangesAsync();
        return userId;
    }

    internal static async Task SeedCredentialsAsync(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        Guid userId,
        string username,
        string? passwordHash = null,
        bool signInProvider = false,
        string? internalId = null,
        string? emailCiphertext = null,
        string? emailLookupHash = null,
        string? passwordResetTokenHash = null,
        DateTimeOffset? passwordResetTokenExpireDate = null,
        bool deleted = false
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context
            .Set<UserCredentialsEntity>()
            .Add(
                new UserCredentialsEntity
                {
                    UserId = userId,
                    Username = username,
                    PasswordHash = passwordHash,
                    SignInProvider = signInProvider,
                    InternalId = internalId,
                    EmailCiphertext = emailCiphertext,
                    EmailLookupHash = emailLookupHash,
                    PasswordResetTokenHash = passwordResetTokenHash,
                    PasswordResetTokenExpireDate = passwordResetTokenExpireDate,
                    Deleted = deleted,
                }
            );
        await context.SaveChangesAsync();
    }

    private sealed class FixedSystemClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
