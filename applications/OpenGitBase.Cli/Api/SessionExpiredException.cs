namespace OpenGitBase.Cli.Api;

public sealed class SessionExpiredException : Exception
{
    public SessionExpiredException()
        : base("Session expired — run `ogb auth login`.")
    {
    }
}
