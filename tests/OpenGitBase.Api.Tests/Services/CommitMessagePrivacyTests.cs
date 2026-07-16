using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class CommitMessagePrivacyTests
{
    [Fact]
    public void RedactEmails_SignedOffByTrailer_ReplacesEmailWithPlaceholder()
    {
        var message =
            "fix the bug\n\nSigned-off-by: Peter Esser <me@peter-esser.de>";

        var redacted = CommitMessagePrivacy.RedactEmails(message);

        Assert.Equal(
            "fix the bug\n\nSigned-off-by: Peter Esser <***@***>",
            redacted
        );
    }

    [Fact]
    public void RedactEmails_CoAuthoredByAndBareEmail_ReplacesAllEmails()
    {
        var message =
            "feat: ship it\n\n"
            + "Co-authored-by: Ada Lovelace <ada@example.com>\n"
            + "Contact: support@example.org";

        var redacted = CommitMessagePrivacy.RedactEmails(message);

        Assert.Equal(
            "feat: ship it\n\n"
                + "Co-authored-by: Ada Lovelace <***@***>\n"
                + "Contact: ***@***",
            redacted
        );
    }

    [Fact]
    public void RedactEmails_MessageWithoutEmails_ReturnsUnchanged()
    {
        const string message = "refactor helpers\n\nNo trailers here.";

        Assert.Equal(message, CommitMessagePrivacy.RedactEmails(message));
    }

    [Fact]
    public void RedactEmails_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CommitMessagePrivacy.RedactEmails(string.Empty));
    }
}
