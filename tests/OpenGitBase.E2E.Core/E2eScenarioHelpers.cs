using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenGitBase.E2E.Core;

public static class E2eScenarioHelpers
{
    public static string ParseJwtToken(HttpCapture loginResponse) =>
        loginResponse.Body.Trim('"');

    public static string ParsePatToken(HttpCapture patResponse)
    {
        using var doc = JsonDocument.Parse(patResponse.Body);
        if (doc.RootElement.TryGetProperty("token", out var token))
        {
            return token.GetString() ?? throw new InvalidOperationException("PAT token missing in response.");
        }

        throw new InvalidOperationException($"PAT response missing token field: {patResponse.Body}");
    }

    public static string ParseRepositoryId(HttpCapture createResponse)
    {
        using var doc = JsonDocument.Parse(createResponse.Body);
        if (doc.RootElement.ValueKind == JsonValueKind.String)
        {
            return doc.RootElement.GetString() ?? string.Empty;
        }

        if (doc.RootElement.TryGetProperty("value", out var value))
        {
            return value.GetString() ?? value.ToString();
        }

        return createResponse.Body.Trim('"');
    }

    public static string ParseVerificationCode(string htmlBody)
    {
        var match = Regex.Match(htmlBody, @"verification code is <strong>([^<]+)</strong>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(htmlBody, @"<strong>([A-Za-z0-9]{6,})</strong>");
        }

        if (!match.Success)
        {
            throw new InvalidOperationException("Could not parse verification code from captured email HTML.");
        }

        return match.Groups[1].Value;
    }

    public static string ParseDiscussionNumber(HttpCapture createResponse)
    {
        using var doc = JsonDocument.Parse(createResponse.Body);
        return doc.RootElement.GetProperty("number").GetInt32().ToString();
    }

    public static async Task WaitForStorageProvisioningAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
    }

    public static string ExtractUserIdFromJwt(string jwt)
    {
        var parts = jwt.Trim('"').Split('.');
        if (parts.Length < 2)
        {
            return string.Empty;
        }

        var payload = parts[1];
        payload += new string('=', (4 - (payload.Length % 4)) % 4);
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.TryGetProperty("identityproviderid", out var id)
            ? id.GetString() ?? string.Empty
            : string.Empty;
    }
}
