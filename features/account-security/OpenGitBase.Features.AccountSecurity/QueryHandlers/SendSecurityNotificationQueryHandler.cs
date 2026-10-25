using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Events;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class SendSecurityNotificationQueryHandler : IQueryHandler<SendSecurityNotificationQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _protection;
    private readonly IQueryProcessor _queryProcessor;
    private readonly TwoFactorOptions _options;
    private readonly ILogger<SendSecurityNotificationQueryHandler> _logger;

    public SendSecurityNotificationQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService protection,
        IQueryProcessor queryProcessor,
        TwoFactorOptions options,
        ILogger<SendSecurityNotificationQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _protection = protection;
        _queryProcessor = queryProcessor;
        _options = options;
        _logger = logger;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        SendSecurityNotificationQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == query.UserId.Value && !c.Deleted, cancellationToken);
        if (credentials is null)
        {
            return Option<Unit>.None;
        }

        if (string.IsNullOrEmpty(credentials.EmailCiphertext))
        {
            return Unit.Value;
        }

        // The event already happened: neither an aborted request nor a failing mail service may undo it.
        try
        {
            var unusedRecoveryCodes = query.Notification == SecurityNotification.RecoveryCodeUsed
                ? await RecoveryCodes.CountUnusedAsync(context, credentials.UserId, CancellationToken.None)
                : 0;
            var (subject, text) = Describe(query.Notification, unusedRecoveryCodes);
            var sent = await _queryProcessor
                .RunQueryAsync(
                    new EmailSendQuery
                    {
                        To = new EmailAddress
                        {
                            Email = _protection.DecryptEmail(credentials.EmailCiphertext),
                            Name = credentials.Username,
                        },
                        Subject = subject,
                        HtmlBody = Body(credentials.Username, text, query),
                    },
                    CancellationToken.None
                )
                .ConfigureAwait(false);
            if (sent.IsNone)
            {
                LogFailure(query, "the email service did not send it");
            }
        }
        catch (Exception exception)
        {
            LogFailure(query, exception.Message);
        }

        return Unit.Value;
    }

    private static (string Subject, string Text) Describe(SecurityNotification notification, int unusedRecoveryCodes) =>
        notification switch
        {
            SecurityNotification.TwoFactorEnabled => (
                "Two-factor authentication turned on",
                "Two-factor authentication was turned on for your account. Signing in with your password now needs a second step."
            ),
            SecurityNotification.TwoFactorDisabled => (
                "Two-factor authentication turned off",
                "Two-factor authentication was turned off for your account. Signing in with your password no longer needs a second step."
            ),
            SecurityNotification.PasskeyAdded => (
                "Passkey added to your account",
                "A new passkey was added to your account. It can sign in to your account."
            ),
            SecurityNotification.RecoveryCodeUsed => (
                "Recovery code used to sign in",
                "A recovery code was used to sign in to your account. Each code works once; "
                    + $"you have {unusedRecoveryCodes} unused recovery codes left."
            ),
            SecurityNotification.SecondFactorLockedOut => (
                "Sign-in blocked after failed second steps",
                "Your password was entered correctly, but the second sign-in step failed "
                    + $"{SecondFactorAttempts.MaxConsecutiveFailures} times in a row. The second step is blocked for "
                    + $"{SecondFactorAttempts.LockoutDuration.TotalMinutes:0} minutes. If this was not you, someone knows "
                    + "your password: change it."
            ),
            SecurityNotification.TwoFactorReset => (
                "Two-factor authentication reset by an administrator",
                "An administrator turned off two-factor authentication for your account and removed your authenticator "
                    + "app, recovery codes and passkeys. You can sign in with your password and set it up again."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(notification), notification, null),
        };

    private string Body(string username, string text, SendSecurityNotificationQuery query)
    {
        var settingsUrl = WebUtility.HtmlEncode(_options.SecuritySettingsUrl);
        var time = query.OccurredAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var remoteIp = string.IsNullOrEmpty(query.RemoteIpAddress)
            ? string.Empty
            : $"<br>IP address: {WebUtility.HtmlEncode(query.RemoteIpAddress)}";
        return $"Hello {WebUtility.HtmlEncode(username)},<br><br>{text}<br><br>Time: {time} UTC{remoteIp}<br><br>"
            + $"If this was not you, change your password and check your security settings: <a href=\"{settingsUrl}\">{settingsUrl}</a>";
    }

    private void LogFailure(SendSecurityNotificationQuery query, string reason) =>
        SecurityEventLog.Write(
            _logger,
            SecurityEvent.NotificationFailed,
            query.UserId.Value,
            query.RemoteIpAddress,
            $"notification={query.Notification} reason={reason}"
        );
}
