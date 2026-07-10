using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Entities;

public class StatusSnapshotEntity : IIdentifiableEntity<Guid>
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset CheckedAt { get; set; }

    public PublicHealthStatus OverallStatus { get; set; }
}
