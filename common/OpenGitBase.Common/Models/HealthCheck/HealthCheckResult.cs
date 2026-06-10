using System.Text.Json.Serialization;

namespace OpenGitBase.Common.Models.HealthCheck;

public class HealthCheckResult
{
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HealthStatus Status { get; set; }

    public string? Description { get; set; }

    public long DurationMs { get; set; }

    public Dictionary<string, object> Data { get; set; } = new();

    public string? Exception { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
