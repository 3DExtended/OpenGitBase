using OpenGitBase.Cqrs;

namespace OpenGitBase.Common.SendGrid;

public class EmailSendQuery : IQuery<Unit, EmailSendQuery>
{
    public required EmailAddress To { get; set; }

    public List<EmailAddress> CC { get; set; } = [];

    public List<EmailAddress> BCC { get; set; } = [];

    public required string Subject { get; set; }

    public required string HtmlBody { get; set; }
}
