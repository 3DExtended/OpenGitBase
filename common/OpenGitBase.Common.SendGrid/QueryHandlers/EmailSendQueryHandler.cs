using Microsoft.Extensions.Logging;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Common.SendGrid.QueryHandlers;

public class EmailSendQueryHandler : IQueryHandler<EmailSendQuery, Unit>
{
    private readonly SendGridOptions _options;
    private readonly ISendGridEmailSender _emailSender;
    private readonly ILogger<EmailSendQueryHandler>? _logger;

    public EmailSendQueryHandler(
        SendGridOptions options,
        ISendGridEmailSender emailSender,
        ILogger<EmailSendQueryHandler>? logger = null
    )
    {
        _options = options;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        EmailSendQuery query,
        CancellationToken cancellationToken
    )
    {
        if (_options.IsDisabled && !IsE2eCaptureMode())
        {
            _logger?.LogInformation(
                "SendGrid disabled; skipping email to {Recipient} with subject {Subject}",
                query.To.Email,
                query.Subject
            );
            return Unit.Value;
        }

        if (
            string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.FromEmailAddress)
        )
        {
            _logger?.LogError("SendGrid is not configured.");
            return Option<Unit>.None;
        }

        await _emailSender
            .SendAsync(
                new SendGridEmailMessage(
                    _options.ApiKey,
                    _options.FromEmailAddress,
                    _options.FromSenderName,
                    query.To.Email ?? string.Empty,
                    query.To.Name,
                    query.Subject,
                    query.HtmlBody
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        return Unit.Value;
    }

    private bool IsE2eCaptureMode() =>
        string.Equals(_options.ApiKey, "e2e-capture", StringComparison.Ordinal)
        || string.Equals(Environment.GetEnvironmentVariable("E2E__CaptureEmail"), "true", StringComparison.OrdinalIgnoreCase);
}
