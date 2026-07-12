namespace OpenGitBase.Cli.Api;

public sealed class OgbApiException : Exception
{
    public OgbApiException(string message, int httpStatus, string? detail)
        : base(message)
    {
        HttpStatus = httpStatus;
        Detail = detail;
    }

    public int HttpStatus { get; }

    public string? Detail { get; }
}
