using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Totp;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.Tests.Testing;

/// <summary>Account-security handlers on an in-memory SQLite database with a clock the test moves.</summary>
public sealed class AccountSecurityTestScope : IAsyncDisposable
{
    public const string Password = "Password123!";

    /// <summary>The origin the software authenticator reports; the scope's relying party accepts it.</summary>
    public const string WebAuthnOrigin = "http://localhost:8089";

    /// <summary>The page security emails link to in this scope.</summary>
    public const string SecuritySettingsUrl = "https://opengitbase.test/settings/security";

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
        services.AddLogging(builder => builder.AddProvider(Logs));
        services.AddSingleton<IQueryHandler<EmailSendQuery, Unit>>(Emails);
        services.AddSingleton<IRemoteIpAddressAccessor>(RemoteIp);
        services.AddSingleton(new TwoFactorOptions { Enabled = true, SecuritySettingsUrl = SecuritySettingsUrl });
        services.AddSingleton(
            new EncryptionOptions { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "test-pepper" }
        );
        services.AddSingleton<IEmailProtectionService, EmailProtectionService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ISystemClock>(Clock);
        services.AddSingleton(
            new WebAuthnOptions { RelyingPartyId = "localhost", Origins = [WebAuthnOrigin] }
        );

        var mapsterConfig = new TypeAdapterConfig();
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options => options.WithQueryHandlersFrom(handlerAssembly));

        _serviceProvider = services.BuildServiceProvider();
    }

    public TestClock Clock { get; } = new(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));

    /// <summary>Every email the handlers send; set <see cref="RecordingEmailSender.Failure"/> to make sending fail.</summary>
    public RecordingEmailSender Emails { get; } = new();

    public RecordingLoggerProvider Logs { get; } = new();

    public FixedRemoteIpAddress RemoteIp { get; } = new();

    public static async Task<AccountSecurityTestScope> CreateAsync()
    {
        var scope = new AccountSecurityTestScope();
        await using var context = await scope.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        return scope;
    }

    public static string WrongCode(string correctCode) => correctCode == "000000" ? "111111" : "000000";

    public static SoftwarePasskeyAuthenticator NewPasskey() => new(WebAuthnOrigin);

    public Task<OpenGitBaseDbContext> CreateDbContextAsync() =>
        _serviceProvider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>().CreateDbContextAsync();

    public THandler GetHandler<THandler>()
        where THandler : notnull => _serviceProvider.GetRequiredService<THandler>();

    public TService GetService<TService>()
        where TService : notnull => _serviceProvider.GetRequiredService<TService>();

    /// <summary>
    /// A user with a password, or a Google/Apple-only user when <paramref name="withPassword"/> is false. The
    /// address is <c>{username}@example.com</c> unless <paramref name="withEmail"/> is false.
    /// </summary>
    public async Task<UserId> SeedUserAsync(string username = "alice", bool withPassword = true, bool withEmail = true)
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
                EmailCiphertext = withEmail
                    ? GetService<IEmailProtectionService>().EncryptEmail(username + "@example.com")
                    : null,
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

    /// <summary>
    /// Turns 2FA on the way a user does (setup, then confirmation) and returns the recovery codes.
    /// The confirmation spends the current time step, so the next TOTP code needs the clock moved on.
    /// </summary>
    public async Task<IReadOnlyList<string>> EnableTotpWithRecoveryCodesAsync(UserId userId)
    {
        var setup = await GetHandler<StartTotpSetupQueryHandler>()
            .RunQueryAsync(new StartTotpSetupQuery { UserId = userId, Issuer = "OpenGitBase" }, CancellationToken.None);
        var confirmation = await GetHandler<ConfirmTotpSetupQueryHandler>()
            .RunQueryAsync(
                new ConfirmTotpSetupQuery { UserId = userId, Code = CurrentCode(setup.Get().Secret) },
                CancellationToken.None
            );
        return confirmation.Get().RecoveryCodes!;
    }

    /// <summary>Turns 2FA on with the user's passkeys alone, as the security page does; returns the recovery codes.</summary>
    public async Task<IReadOnlyList<string>> EnableTwoFactorWithPasskeysAsync(UserId userId)
    {
        var activation = await GetHandler<EnableTwoFactorWithPasskeysQueryHandler>()
            .RunQueryAsync(new EnableTwoFactorWithPasskeysQuery { UserId = userId }, CancellationToken.None);
        return activation.Get().RecoveryCodes!;
    }

    /// <summary>Registers <paramref name="passkey"/> the way the browser does: options, then the attestation.</summary>
    public async Task<PasskeyRegistrationResult> RegisterPasskeyAsync(
        UserId userId,
        SoftwarePasskeyAuthenticator passkey,
        string name = "Laptop"
    )
    {
        var options = await GetHandler<StartPasskeyRegistrationQueryHandler>()
            .RunQueryAsync(new StartPasskeyRegistrationQuery { UserId = userId }, CancellationToken.None);
        var result = await GetHandler<CompletePasskeyRegistrationQueryHandler>()
            .RunQueryAsync(
                new CompletePasskeyRegistrationQuery
                {
                    UserId = userId,
                    Name = name,
                    AttestationResponseJson = passkey.CreateCredential(options.Get().OptionsJson),
                },
                CancellationToken.None
            );
        return result.Get();
    }

    public async Task<List<UserPasskeyEntity>> GetPasskeysAsync(UserId userId)
    {
        await using var context = await CreateDbContextAsync();
        return await context.Set<UserPasskeyEntity>().AsNoTracking().Where(p => p.UserId == userId.Value).ToListAsync();
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

    public async Task<List<UserRecoveryCodeEntity>> GetRecoveryCodesAsync(UserId userId)
    {
        await using var context = await CreateDbContextAsync();
        return await context.Set<UserRecoveryCodeEntity>().AsNoTracking().Where(r => r.UserId == userId.Value).ToListAsync();
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
