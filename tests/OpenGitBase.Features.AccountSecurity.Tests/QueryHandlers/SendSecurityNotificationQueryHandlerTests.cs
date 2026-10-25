using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class SendSecurityNotificationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_EmailsTheAccount_WithTimeRemoteIpAndSettingsLink()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync("alice");

        var result = await SendAsync(scope, userId, SecurityNotification.TwoFactorEnabled, remoteIp: "198.51.100.23");

        QueryHandlerResultAssert.AssertSome(result);
        var email = Assert.Single(scope.Emails.Sent);
        Assert.Equal("alice@example.com", email.To.Email);
        Assert.Equal("alice", email.To.Name);
        Assert.Equal("Two-factor authentication turned on", email.Subject);
        Assert.Contains("Hello alice,", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("2027-01-15 08:00 UTC", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("198.51.100.23", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(
            $"<a href=\"{AccountSecurityTestScope.SecuritySettingsUrl}\">",
            email.HtmlBody,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(SecurityNotification.TwoFactorEnabled, "Two-factor authentication turned on")]
    [InlineData(SecurityNotification.TwoFactorDisabled, "Two-factor authentication turned off")]
    [InlineData(SecurityNotification.PasskeyAdded, "Passkey added to your account")]
    [InlineData(SecurityNotification.RecoveryCodeUsed, "Recovery code used to sign in")]
    [InlineData(SecurityNotification.SecondFactorLockedOut, "Sign-in blocked after failed second steps")]
    [InlineData(SecurityNotification.TwoFactorReset, "Two-factor authentication reset by an administrator")]
    public async Task RunQueryAsync_SendsOneEmailPerNotification(SecurityNotification notification, string subject)
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        await SendAsync(scope, userId, notification);

        Assert.Equal(subject, Assert.Single(scope.Emails.Sent).Subject);
    }

    [Fact]
    public async Task RunQueryAsync_SecondFactorLockedOut_SaysThePasswordWasRight()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        await SendAsync(scope, userId, SecurityNotification.SecondFactorLockedOut);

        var body = Assert.Single(scope.Emails.Sent).HtmlBody;
        Assert.Contains("password was entered correctly", body, StringComparison.Ordinal);
        Assert.Contains("15 minutes", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunQueryAsync_RecoveryCodeUsed_SaysHowManyCodesAreLeft()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpWithRecoveryCodesAsync(userId);

        await SendAsync(scope, userId, SecurityNotification.RecoveryCodeUsed);

        Assert.Contains("10 unused recovery codes", scope.Emails.Sent[^1].HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunQueryAsync_WithoutRemoteIp_LeavesTheAddressOut()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        await SendAsync(scope, userId, SecurityNotification.TwoFactorReset, remoteIp: null);

        Assert.DoesNotContain("IP address", Assert.Single(scope.Emails.Sent).HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunQueryAsync_AccountWithoutEmail_SendsNothing()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync(withEmail: false);

        var result = await SendAsync(scope, userId, SecurityNotification.TwoFactorEnabled);

        QueryHandlerResultAssert.AssertSome(result);
        Assert.Empty(scope.Emails.Sent);
    }

    [Theory]
    [InlineData(EmailSenderFailure.ReturnsNone)]
    [InlineData(EmailSenderFailure.Throws)]
    public async Task RunQueryAsync_SendingFails_StillSucceedsAndLogsTheFailure(EmailSenderFailure failure)
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        scope.Emails.Failure = failure;

        var result = await SendAsync(scope, userId, SecurityNotification.SecondFactorLockedOut);

        QueryHandlerResultAssert.AssertSome(result);
        var logged = Assert.Single(scope.Logs.Named("security.notification.failed"));
        Assert.Equal(userId.Value, logged.Properties["UserId"]);
    }

    [Fact]
    public async Task RunQueryAsync_UnknownUser_ReturnsNone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();

        var result = await SendAsync(scope, UserId.From(Guid.NewGuid()), SecurityNotification.TwoFactorEnabled);

        Assert.True(result.IsNone);
        Assert.Empty(scope.Emails.Sent);
    }

    private static Task<Option<Unit>> SendAsync(
        AccountSecurityTestScope scope,
        UserId userId,
        SecurityNotification notification,
        string? remoteIp = "203.0.113.7"
    ) =>
        scope
            .GetHandler<SendSecurityNotificationQueryHandler>()
            .RunQueryAsync(
                new SendSecurityNotificationQuery
                {
                    UserId = userId,
                    Notification = notification,
                    OccurredAt = scope.Clock.UtcNow,
                    RemoteIpAddress = remoteIp,
                },
                CancellationToken.None
            );
}
