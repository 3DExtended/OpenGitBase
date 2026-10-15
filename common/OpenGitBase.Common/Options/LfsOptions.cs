namespace OpenGitBase.Common.Options;

/// <summary>Git LFS settings (section <c>Lfs</c>).</summary>
public sealed class LfsOptions
{
    /// <summary>Off by default: the batch endpoint answers 501 until an operator switches LFS on.</summary>
    public bool Enabled { get; set; }

    /// <summary>How long an upload or download action may wait before its transfer starts.</summary>
    public int ActionTokenLifetimeSeconds { get; set; } = 3600;

    /// <summary>
    /// Base URL for action hrefs, such as <c>https://git.example.com</c>. Empty means the URL the client used
    /// to reach the dispatcher.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
