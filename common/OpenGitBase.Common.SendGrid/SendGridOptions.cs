namespace OpenGitBase.Common.SendGrid;

public class SendGridOptions
{
    public string? ApiKey { get; set; }

    public string? FromEmailAddress { get; set; }

    public string? FromSenderName { get; set; }

    public bool IsDisabled { get; set; }
}
