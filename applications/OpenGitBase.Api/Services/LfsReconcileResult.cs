namespace OpenGitBase.Api.Services;

/// <summary>
/// What one LFS reconcile run of a repository found and did: copies missing on reachable nodes, repaired, failed to
/// repair, and left for the next run because of <c>Lfs:MaxRepairsPerRun</c>.
/// </summary>
public sealed record LfsReconcileResult(int Missing, int Repaired, int Failed, int Remaining)
{
    public static LfsReconcileResult Nothing { get; } = new(0, 0, 0, 0);
}
