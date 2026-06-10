using Microsoft.Extensions.Logging;
using OpenGitBase.Cqrs;
using SendGrid;
using SendGrid.Helpers.Mail;
using MailAddress = SendGrid.Helpers.Mail.EmailAddress;

namespace OpenGitBase.Common.SendGrid.QueryHandlers;

public class EmailSendQueryHandler : IQueryHandler<EmailSendQuery, Unit>
{
    private readonly SendGridOptions _options;
    private readonly ILogger<EmailSendQueryHandler>? _logger;

    public EmailSendQueryHandler(
        SendGridOptions options,
        ILogger<EmailSendQueryHandler>? logger = null
    )
    {
        _options = options;
        _logger = logger;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        EmailSendQuery query,
        CancellationToken cancellationToken
    )
    {
        if (_options.IsDisabled)
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

        var client = new SendGridClient(_options.ApiKey);
        var from = new MailAddress(
            _options.FromEmailAddress,
            _options.FromSenderName ?? string.Empty
        );
        var to = new MailAddress(query.To.Email ?? string.Empty, query.To.Name ?? string.Empty);
        var msg = MailHelper.CreateSingleEmail(
            from,
            to,
            query.Subject,
            query.HtmlBody,
            query.HtmlBody
        );
        await client.SendEmailAsync(msg, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
