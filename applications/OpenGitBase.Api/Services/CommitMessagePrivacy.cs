using System.Text.RegularExpressions;

namespace OpenGitBase.Api.Services;

public static partial class CommitMessagePrivacy
{
    private const string RedactedEmail = "***@***";

    public static string RedactEmails(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        return EmailAddress().Replace(message, RedactedEmail);
    }

    // Local-part @ domain with at least one dot in the domain (avoids bare @mentions).
    [GeneratedRegex(
        @"[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex EmailAddress();
}
