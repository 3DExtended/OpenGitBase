namespace OpenGitBase.Dispatcher.Models;

public enum LfsRequestKind
{
    /// <summary>A path under <c>info/lfs</c> this server does not offer.</summary>
    Unsupported,
    Batch,
    Upload,
    Download,
    Verify,
    LocksVerify,
}
