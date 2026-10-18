using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Totp;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.Tests.Testing;

/// <summary>Account-security handlers on an in-memory SQLite database with a clock the test moves.</summary>
public sealed class AccountSecurityTestScope : IAsyncDisposable
{
    public const string Password = "Password123!";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    private AccountSecurityTestScope()
    {
        _connection = SqliteTestConnection.OpenInMemory();

        var handlerAssembly = typeof(AccountSecurityMapsterConfig).Assembly;
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([handlerAssembly, typeof(UsersMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(_connection);
        services.AddLogging();
        services.AddSingleton(
            new EncryptionOptions { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "test-pepper" }
        );
        services.AddSingleton<IEmailProtectionService, EmailProtectionService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ISystemClock>(Clock);

        var mapsterConfig = new TypeAdapterConfig();
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options => options.WithQueryHandlersFrom(handlerAssembly));

        _serviceProvider = services.BuildServiceProvider();
    }

    public TestClock Clock { get; } = new(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));

    public static async Task<AccountSecurityTestScope> CreateAsync()
    {
        var scope = new AccountSecurityTestScope();
        await using var context = await scope.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        return scope;
    }

    public static string WrongCode(string correctCode) => correctCode == "000000" ? "111111" : "000000";

    public Task<OpenGitBaseDbContext> CreateDbContextAsync() =>
        _serviceProvider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>().CreateDbContextAsync();

    public THandler GetHandler<THandler>()
        where THandler : notnull => _serviceProvider.GetRequiredService<THandler>();

    public TService GetService<TService>()
        where TService : notnull => _serviceProvider.GetRequiredService<TService>();

    /// <summary>A user with a password, or a Google/Apple-only user when <paramref name="withPassword"/> is false.</summary>
    public async Task<UserId> SeedUserAsync(string username = "alice", bool withPassword = true)
    {
        var userId = Guid.NewGuid();
        await using var context = await CreateDbContextAsync();
        context.Set<UserEntity>().Add(
            new UserEntity
            {
                Id = userId,
                Username = username,
                NormalizedUsername = username.ToLowerInvariant(),
                CreatedAt = Clock.UtcNow,
            }
        );
        context.Set<UserCredentialsEntity>().Add(
            new UserCredentialsEntity
            {
                UserId = userId,
                Username = username,
                PasswordHash = withPassword ? GetService<IPasswordHasherService>().HashPassword(Password) : null,
                SignInProvider = !withPassword,
                InternalId = withPassword ? null : "google-" + username,
            }
        );
        await context.SaveChangesAsync();
        return UserId.From(userId);
    }

    /// <summary>A user who already turned 2FA on with an authenticator app; returns the app's secret.</summary>
    public async Task<string> EnableTotpAsync(UserId userId)
    {
        var secret = TotpAlgorithm.GenerateSecret();
        await using var context = await CreateDbContextAsync();
        context.Set<UserTotpAuthenticatorEntity>().Add(
            new UserTotpAuthenticatorEntity
            {
                UserId = userId.Value,
                SecretProtected = GetService<IEmailProtectionService>().EncryptSecret(secret),
                ConfirmedAt = Clock.UtcNow,
                CreatedAt = Clock.UtcNow,
            }
        );
        var credentials = await context.Set<UserCredentialsEntity>().SingleAsync(c => c.UserId == userId.Value);
        credentials.TwoFactorEnabledAt = Clock.UtcNow;
        await context.SaveChangesAsync();
        return secret;
    }

    public string CurrentCode(string secret) =>
        TotpAlgorithm.ComputeCode(TotpAlgorithm.DecodeSecret(secret), Clock.UtcNow.ToUnixTimeSeconds() / 30);

    public async Task<UserCredentialsEntity> GetCredentialsAsync(UserId userId)
    {
        await using var context = await CreateDbContextAsync();
        return await context.Set<UserCredentialsEntity>().AsNoTracking().SingleAsync(c => c.UserId == userId.Value);
    }

    public async Task<UserTotpAuthenticatorEntity?> GetAuthenticatorAsync(UserId userId)
    {
        await using var context = await CreateDbContextAsync();
        return await context
            .Set<UserTotpAuthenticatorEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.UserId == userId.Value);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    public sealed class TestClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
