namespace OpenGitBase.Features.Status.Contracts;

public sealed class PublicStatusHistoryDayDto
{
    public DateOnly Date { get; set; }

    public double UptimePercent { get; set; }

    public double HealthyRatio { get; set; }

    public double DegradedRatio { get; set; }

    public double UnhealthyRatio { get; set; }
}
