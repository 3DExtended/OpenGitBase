namespace OpenGitBase.Common.SendGrid;

public sealed record SendGridEmailMessage(
    string ApiKey,
    string FromEmailAddress,
    string? FromSenderName,
    string ToEmail,
    string? ToName,
    string Subject,
    string HtmlBody
);
