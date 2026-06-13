namespace OpenGitBase.Common.SendGrid;

public interface ISendGridEmailSender
{
    Task SendAsync(SendGridEmailMessage message, CancellationToken cancellationToken);
}
