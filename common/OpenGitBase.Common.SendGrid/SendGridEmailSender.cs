using SendGrid;
using SendGrid.Helpers.Mail;
using MailAddress = SendGrid.Helpers.Mail.EmailAddress;

namespace OpenGitBase.Common.SendGrid;

public class SendGridEmailSender : ISendGridEmailSender
{
    public Task SendAsync(SendGridEmailMessage message, CancellationToken cancellationToken)
    {
        var client = new SendGridClient(message.ApiKey);
        var from = new MailAddress(message.FromEmailAddress, message.FromSenderName ?? string.Empty);
        var to = new MailAddress(message.ToEmail, message.ToName ?? string.Empty);
        var msg = MailHelper.CreateSingleEmail(
            from,
            to,
            message.Subject,
            message.HtmlBody,
            message.HtmlBody
        );
        return client.SendEmailAsync(msg, cancellationToken);
    }
}
