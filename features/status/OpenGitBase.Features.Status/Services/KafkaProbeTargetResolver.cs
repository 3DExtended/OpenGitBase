namespace OpenGitBase.Features.Status.Services;

public static class KafkaProbeTargetResolver
{
    private const int DefaultPort = 9092;

    public static IReadOnlyList<DataStoreProbeTarget> Resolve(string? bootstrapServers)
    {
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            return Array.Empty<DataStoreProbeTarget>();
        }

        var targets = new List<DataStoreProbeTarget>();
        foreach (var segment in bootstrapServers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseBroker(segment, out var host, out var port))
            {
                continue;
            }

            targets.Add(new DataStoreProbeTarget(host, host, port));
        }

        return targets;
    }

    private static bool TryParseBroker(string segment, out string host, out int port)
    {
        host = string.Empty;
        port = DefaultPort;

        var lastColon = segment.LastIndexOf(':');
        if (lastColon <= 0 || lastColon == segment.Length - 1)
        {
            host = segment.Trim();
            return !string.IsNullOrWhiteSpace(host);
        }

        host = segment[..lastColon].Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (!int.TryParse(segment[(lastColon + 1)..], out port) || port <= 0)
        {
            port = DefaultPort;
        }

        return true;
    }
}
