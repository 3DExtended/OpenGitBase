using OpenGitBase.Common.SendGrid;

namespace OpenGitBase.Common.Tests.SendGrid;

public class CapturingSendGridEmailSenderTests
{
    [Fact]
    public async Task SendAsync_StoresFullMessageInStore()
    {
        var store = new CapturingEmailStore();
        var sender = new CapturingSendGridEmailSender(store);

        await sender.SendAsync(
            new SendGridEmailMessage(
                "key",
                "from@example.com",
                "OpenGitBase",
                "user@example.com",
                "User",
                "Verify email",
                "<p>Your code is <strong>ABC123</strong></p>"),
            CancellationToken.None);

        var messages = store.GetByRecipient("user@example.com");
        Assert.Single(messages);
        Assert.Equal("Verify email", messages[0].Subject);
        Assert.Contains("ABC123", messages[0].HtmlBody, StringComparison.Ordinal);
    }
}
