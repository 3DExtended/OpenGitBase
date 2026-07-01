namespace OpenGitBase.Common.SendGrid;

public sealed class CapturingSendGridEmailSender : ISendGridEmailSender
{
    private readonly CapturingEmailStore _store;

    public CapturingSendGridEmailSender(CapturingEmailStore store)
    {
        _store = store;
    }

    public Task SendAsync(SendGridEmailMessage message, CancellationToken cancellationToken)
    {
        _store.Add(new CapturedEmailMessage
        {
            To = message.ToEmail,
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
        });
        return Task.CompletedTask;
    }
}
