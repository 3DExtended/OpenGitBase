using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Entities;

public class StatusHistoryHourlyBucketEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public StatusComponentGroup ComponentGroup { get; set; }

    public DateTimeOffset PeriodStartUtc { get; set; }

    public int HealthySamples { get; set; }

    public int DegradedSamples { get; set; }

    public int UnhealthySamples { get; set; }

    public int TotalSamples { get; set; }
}
