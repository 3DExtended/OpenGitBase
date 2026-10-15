namespace OpenGitBase.Features.Pipeline.Services;

public sealed class RepositoryFileUnavailableException : Exception
{
    public RepositoryFileUnavailableException(string message)
        : base(message)
    {
    }

    public RepositoryFileUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
