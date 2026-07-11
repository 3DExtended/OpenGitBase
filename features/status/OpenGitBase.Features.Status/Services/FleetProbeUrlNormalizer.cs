namespace OpenGitBase.Features.Status.Services;

internal static class FleetProbeUrlNormalizer
{
    public static string Normalize(string instanceId, string probeUrl)
    {
        var trimmed = probeUrl.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        if (
            !trimmed.Contains("127.0.0.1", StringComparison.Ordinal)
            && !trimmed.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        )
        {
            return trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return $"http://{instanceId}/health";
        }

        var port = uri.IsDefaultPort ? 8080 : uri.Port;
        var path = string.IsNullOrEmpty(uri.AbsolutePath) ? "/health" : uri.AbsolutePath;
        return $"http://{instanceId}:{port}{path}";
    }
}
