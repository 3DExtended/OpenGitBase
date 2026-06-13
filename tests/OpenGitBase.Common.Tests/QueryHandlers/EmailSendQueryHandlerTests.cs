using NSubstitute;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.SendGrid.QueryHandlers;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Common.Tests.QueryHandlers;

public class EmailSendQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenDisabled_ReturnsUnitWithoutSending()
    {
        var sender = Substitute.For<ISendGridEmailSender>();
        var handler = new EmailSendQueryHandler(new SendGridOptions { IsDisabled = true }, sender);

        var result = await handler.RunQueryAsync(
            new EmailSendQuery
            {
                To = new EmailAddress { Email = "user@example.com", Name = "User" },
                Subject = "Hello",
                HtmlBody = "<p>Hi</p>",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task RunQueryAsync_WhenNotConfigured_ReturnsNone()
    {
        var sender = Substitute.For<ISendGridEmailSender>();
        var handler = new EmailSendQueryHandler(
            new SendGridOptions { ApiKey = string.Empty, FromEmailAddress = string.Empty },
            sender
        );

        var result = await handler.RunQueryAsync(
            new EmailSendQuery
            {
                To = new EmailAddress { Email = "user@example.com" },
                Subject = "Hello",
                HtmlBody = "<p>Hi</p>",
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task RunQueryAsync_WhenConfigured_SendsEmail()
    {
        var sender = Substitute.For<ISendGridEmailSender>();
        var handler = new EmailSendQueryHandler(
            new SendGridOptions
            {
                ApiKey = "key",
                FromEmailAddress = "from@example.com",
                FromSenderName = "Sender",
            },
            sender
        );

        var result = await handler.RunQueryAsync(
            new EmailSendQuery
            {
                To = new EmailAddress { Email = "to@example.com", Name = "Recipient" },
                Subject = "Subject",
                HtmlBody = "<p>Body</p>",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        await sender
            .Received(1)
            .SendAsync(
                Arg.Is<SendGridEmailMessage>(message =>
                    message.ApiKey == "key"
                    && message.FromEmailAddress == "from@example.com"
                    && message.ToEmail == "to@example.com"
                    && message.Subject == "Subject"
                ),
                CancellationToken.None
            );
    }
}
