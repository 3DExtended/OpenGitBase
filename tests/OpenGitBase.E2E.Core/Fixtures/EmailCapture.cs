using System.Text.Json;

namespace OpenGitBase.E2E.Core.Fixtures;

public sealed class CapturedEmailMessage
{
    public string To { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string HtmlBody { get; init; } = string.Empty;
}

public static class EmailCapture
{
    public static async Task<IReadOnlyList<CapturedEmailMessage>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = E2eEnvironment.ApiBaseUrl };
        using var response = await client.GetAsync("internal/e2e/emails", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<CapturedEmailMessage>();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CapturedEmailMessage>();
        }

        var messages = new List<CapturedEmailMessage>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            messages.Add(new CapturedEmailMessage
            {
                To = item.TryGetProperty("to", out var to) ? to.GetString() ?? string.Empty : string.Empty,
                Subject = item.TryGetProperty("subject", out var subject) ? subject.GetString() ?? string.Empty : string.Empty,
                HtmlBody = item.TryGetProperty("htmlBody", out var html) ? html.GetString() ?? string.Empty : string.Empty,
            });
        }

        return messages;
    }

    public static async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = E2eEnvironment.ApiBaseUrl };
        await client.PostAsync("internal/e2e/emails/clear", null, cancellationToken).ConfigureAwait(false);
    }

    public static string? FindVerificationCode(IReadOnlyList<CapturedEmailMessage> messages, string recipientEmail)
    {
        var match = messages.LastOrDefault(m =>
            m.To.Contains(recipientEmail, StringComparison.OrdinalIgnoreCase));
        return match == null ? null : TryParseVerificationCode(match.HtmlBody);
    }

    public static string? TryParseVerificationCode(string htmlBody)
    {
        try
        {
            return E2eScenarioHelpers.ParseVerificationCode(htmlBody);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
