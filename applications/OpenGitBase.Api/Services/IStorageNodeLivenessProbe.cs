namespace OpenGitBase.Api.Services;

/// <summary>
/// Asks a storage node directly whether it is up. Health in the database rests on heartbeats, which also stop
/// when the API or the database cannot take them; failover checks with the node before it acts on that.
/// </summary>
public interface IStorageNodeLivenessProbe
{
    /// <summary>True when the node's internal HTTP server answers, with any status.</summary>
    Task<bool> AnswersAsync(string internalHost, int internalHttpPort, CancellationToken cancellationToken);
}
