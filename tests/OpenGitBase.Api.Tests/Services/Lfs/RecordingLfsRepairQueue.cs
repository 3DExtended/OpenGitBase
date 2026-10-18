using System.Collections.Concurrent;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services.Lfs;

/// <summary>Remembers which repositories were queued for an LFS repair, in order.</summary>
public sealed class RecordingLfsRepairQueue : ILfsRepairQueue
{
    private readonly ConcurrentQueue<Guid> _scheduled = new();

    public IReadOnlyCollection<Guid> Scheduled => _scheduled;

    public void Schedule(Guid repositoryId) => _scheduled.Enqueue(repositoryId);
}
