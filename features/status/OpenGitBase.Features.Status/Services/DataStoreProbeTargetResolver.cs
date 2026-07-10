using System.Text.RegularExpressions;

namespace OpenGitBase.Features.Status.Services;

public static partial class DataStoreProbeTargetResolver
{
    public static IReadOnlyList<DataStoreProbeTarget> Resolve(
        string? postgresConnectionString,
        string? redisUrl
    )
    {
        var targets = new List<DataStoreProbeTarget>();
        if (TryParsePostgres(postgresConnectionString, out var postgresHost, out var postgresPort))
        {
            targets.Add(new DataStoreProbeTarget("postgres", postgresHost, postgresPort));
        }

        if (TryParseRedis(redisUrl, out var redisHost, out var redisPort))
        {
            targets.Add(new DataStoreProbeTarget("redis", redisHost, redisPort));
        }

        return targets;
    }

    private static bool TryParsePostgres(
        string? connectionString,
        out string host,
        out int port
    )
    {
        host = "postgres";
        port = 5432;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var hostMatch = PostgresHostRegex().Match(connectionString);
        if (hostMatch.Success)
        {
            host = hostMatch.Groups[1].Value;
        }

        var portMatch = PostgresPortRegex().Match(connectionString);
        if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out var parsedPort))
        {
            port = parsedPort;
        }

        return true;
    }

    private static bool TryParseRedis(string? redisUrl, out string host, out int port)
    {
        host = "redis";
        port = 6379;
        if (string.IsNullOrWhiteSpace(redisUrl))
        {
            return false;
        }

        if (Uri.TryCreate(redisUrl, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
            port = uri.Port > 0 ? uri.Port : 6379;
            return true;
        }

        return false;
    }

    [GeneratedRegex("Host=([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PostgresHostRegex();

    [GeneratedRegex("Port=([0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PostgresPortRegex();
}
