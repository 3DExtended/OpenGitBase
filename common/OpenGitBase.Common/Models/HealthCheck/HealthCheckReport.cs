using System.Text.Json.Serialization;

namespace OpenGitBase.Common.Models.HealthCheck;

public class HealthCheckReport
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HealthStatus Status { get; set; }

    public long TotalDurationMs { get; set; }

    public List<HealthCheckResult> Results { get; set; } = new();

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
