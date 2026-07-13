using OpenGitBase.Cli.Api;
using OpenGitBase.Cli.Output;

namespace OpenGitBase.Cli.Tests;

public sealed class CliErrorHandlerTests
{
    [Fact]
    public void Session_expired_writes_json_error_with_status()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var writer = new JsonOutputWriter(output);

        var exit = CliErrorHandler.HandleException(new SessionExpiredException(), writer, json: true);

        Assert.Equal(CliExitCodes.Error, exit);
        Assert.Contains("\"httpStatus\":401", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Api_exception_writes_detail()
    {
        using var output = new StringWriter();
        var writer = new JsonOutputWriter(output);

        var exit = CliErrorHandler.HandleException(
            new OgbApiException("Forbidden", 403, """{"error":"nope"}"""),
            writer,
            json: true);

        Assert.Equal(CliExitCodes.Error, exit);
        Assert.Contains("\"httpStatus\":403", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("nope", output.ToString(), StringComparison.Ordinal);
    }
}
