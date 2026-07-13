using System.Text.Json;
using OpenGitBase.Cli.Git;

namespace OpenGitBase.Cli.Api;

internal static class OgbApiClientHelpers
{
    public static string BuildDiscussionsPath(RepoSlug repo) =>
        $"repository/by-slug/{repo.Owner}/{repo.Slug}/discussions";

    public static string BuildMergeRequestsPath(RepoSlug repo) =>
        $"repository/by-slug/{repo.Owner}/{repo.Slug}/merge-requests";

    public static string? TryReadErrorDetail(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var errorProperty))
            {
                return errorProperty.GetString();
            }

            if (document.RootElement.TryGetProperty("title", out var titleProperty))
            {
                return titleProperty.GetString();
            }
        }
        catch (JsonException)
        {
            return responseBody;
        }

        return responseBody;
    }
}
