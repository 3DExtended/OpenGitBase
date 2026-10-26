using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class CompletePasskeyRegistrationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ValidAttestation_StoresTheCredential()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        passkey.Synced = true;

        var result = await scope.RegisterPasskeyAsync(userId, passkey, "  MacBook  ");

        Assert.Equal(PasskeyRegistrationStatus.Registered, result.Status);
        Assert.Equal("MacBook", result.Passkey!.Name);
        Assert.True(result.Passkey.IsBackedUp);
        var stored = Assert.Single(await scope.GetPasskeysAsync(userId));
        Assert.Equal(result.Passkey.Id, stored.Id);
        Assert.Equal(passkey.CredentialId, stored.CredentialId);
        Assert.NotEmpty(stored.PublicKey);
        Assert.True(stored.IsBackupEligible);
        Assert.Equal("Internal,Hybrid", stored.Transports);
        Assert.Equal(scope.Clock.UtcNow, stored.CreatedAt);
        Assert.Null(stored.LastUsedAt);
    }

    [Fact]
    public async Task RunQueryAsync_WrongOrigin_IsRejected()
    {
        await AssertRejectedAsync(passkey => passkey.Origin = "https://evil.example", PasskeyRegistrationStatus.Rejected);
    }

    [Fact]
    public async Task RunQueryAsync_WrongRelyingPartyId_IsRejected()
    {
        await AssertRejectedAsync(passkey => passkey.RelyingPartyIdOverride = "evil.example", PasskeyRegistrationStatus.Rejected);
    }

    [Fact]
    public async Task RunQueryAsync_WithoutUserVerification_IsRejected()
    {
        await AssertRejectedAsync(passkey => passkey.UserVerified = false, PasskeyRegistrationStatus.Rejected);
    }

    [Fact]
    public async Task RunQueryAsync_ChallengeUsedTwice_IsRejectedTheSecondTime()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var options = await StartAsync(scope, userId);
        using var first = AccountSecurityTestScope.NewPasskey();
        using var second = AccountSecurityTestScope.NewPasskey();

        var accepted = await CompleteAsync(scope, userId, first.CreateCredential(options));
        var replayed = await CompleteAsync(scope, userId, second.CreateCredential(options));

        Assert.Equal(PasskeyRegistrationStatus.Registered, accepted.Status);
        Assert.Equal(PasskeyRegistrationStatus.ChallengeExpired, replayed.Status);
        Assert.Single(await scope.GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_ChallengeOlderThanFiveMinutes_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var options = await StartAsync(scope, userId);
        using var passkey = AccountSecurityTestScope.NewPasskey();
        scope.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var result = await CompleteAsync(scope, userId, passkey.CreateCredential(options));

        Assert.Equal(PasskeyRegistrationStatus.ChallengeExpired, result.Status);
        Assert.Empty(await scope.GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_AnotherUsersChallenge_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var alice = await scope.SeedUserAsync("alice");
        var mallory = await scope.SeedUserAsync("mallory");
        var alicesOptions = await StartAsync(scope, alice);
        using var passkey = AccountSecurityTestScope.NewPasskey();

        var result = await CompleteAsync(scope, mallory, passkey.CreateCredential(alicesOptions));

        Assert.Equal(PasskeyRegistrationStatus.ChallengeExpired, result.Status);
        Assert.Empty(await scope.GetPasskeysAsync(mallory));
    }

    [Fact]
    public async Task RunQueryAsync_SameCredentialTwice_IsAlreadyRegistered()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);

        var again = await scope.RegisterPasskeyAsync(userId, passkey);

        Assert.Equal(PasskeyRegistrationStatus.AlreadyRegistered, again.Status);
        Assert.Single(await scope.GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_NotJson_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await StartAsync(scope, userId);

        var result = await CompleteAsync(scope, userId, "not json");

        Assert.Equal(PasskeyRegistrationStatus.Rejected, result.Status);
    }

    private static async Task AssertRejectedAsync(
        Action<SoftwarePasskeyAuthenticator> breakIt,
        PasskeyRegistrationStatus expected
    )
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        breakIt(passkey);

        var result = await scope.RegisterPasskeyAsync(userId, passkey);

        Assert.Equal(expected, result.Status);
        Assert.Empty(await scope.GetPasskeysAsync(userId));
    }

    private static async Task<string> StartAsync(AccountSecurityTestScope scope, UserId userId) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<StartPasskeyRegistrationQueryHandler>()
                .RunQueryAsync(new StartPasskeyRegistrationQuery { UserId = userId }, CancellationToken.None)
        ).OptionsJson;

    private static async Task<PasskeyRegistrationResult> CompleteAsync(
        AccountSecurityTestScope scope,
        UserId userId,
        string attestationJson
    ) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<CompletePasskeyRegistrationQueryHandler>()
                .RunQueryAsync(
                    new CompletePasskeyRegistrationQuery
                    {
                        UserId = userId,
                        Name = "Laptop",
                        AttestationResponseJson = attestationJson,
                    },
                    CancellationToken.None
                )
        );
}
