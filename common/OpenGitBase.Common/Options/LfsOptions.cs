namespace OpenGitBase.Common.Options;

/// <summary>Git LFS settings (section <c>Lfs</c>).</summary>
public sealed class LfsOptions
{
    /// <summary>Off by default: the batch endpoint answers 501 until an operator switches LFS on.</summary>
    public bool Enabled { get; set; }

    /// <summary>How long an upload or download action may wait before its transfer starts.</summary>
    public int ActionTokenLifetimeSeconds { get; set; } = 3600;

    /// <summary>Largest object an upload batch accepts; bigger ones get a per-object 422.</summary>
    public long MaxObjectBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    /// <summary>Most objects in one batch request (git-lfs sends 100 by default); more answer 413.</summary>
    public int MaxBatchObjects { get; set; } = 100;

    /// <summary>
    /// Base URL for action hrefs, such as <c>https://git.example.com</c>. Empty means the URL the client used
    /// to reach the dispatcher.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
