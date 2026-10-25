namespace OpenGitBase.Api.Services;

/// <summary>An encrypted LFS copy could not be produced or failed authentication; nothing of it may be trusted.</summary>
public sealed class LfsCipherException : Exception
{
    public LfsCipherException(string message)
        : base(message)
    {
    }

    public LfsCipherException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
