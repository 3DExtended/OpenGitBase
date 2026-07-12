using OpenGitBase.Cli.Output;

namespace OpenGitBase.Cli;

public static class CliErrorHandler
{
    public static int HandleException(Exception exception, IOutputWriter outputWriter, bool json)
    {
        _ = json;
        var error = exception switch
        {
            Api.SessionExpiredException sessionExpired => new CliErrorOutput
            {
                Error = sessionExpired.Message,
                HttpStatus = 401,
            },
            Api.OgbApiException apiException => new CliErrorOutput
            {
                Error = apiException.Message,
                HttpStatus = apiException.HttpStatus,
                Detail = apiException.Detail,
            },
            _ => new CliErrorOutput { Error = exception.Message },
        };

        outputWriter.WriteError(error);
        return CliExitCodes.Error;
    }
}
