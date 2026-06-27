using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

public sealed class GitSmartHttpHandler
{
    private const string WwwAuthenticateHeader = "Basic realm=\"OpenGitBase\"";

    private readonly GitSmartHttpPathParser _pathParser;
    private readonly RepositoryAccessCheckClient _accessCheckClient;
    private readonly GitHttpProxyService _gitHttpProxyService;
    private readonly ILogger<GitSmartHttpHandler> _logger;

    public GitSmartHttpHandler(
        GitSmartHttpPathParser pathParser,
        RepositoryAccessCheckClient accessCheckClient,
        GitHttpProxyService gitHttpProxyService,
        ILogger<GitSmartHttpHandler> logger
    )
    {
        _pathParser = pathParser;
        _accessCheckClient = accessCheckClient;
        _gitHttpProxyService = gitHttpProxyService;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!_pathParser.TryParse(
                context.Request.Path,
                context.Request.QueryString,
                out var gitRequest))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!BasicAuthTokenReader.TryReadAccessToken(context.Request, out var accessToken))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = WwwAuthenticateHeader;
            return;
        }

        RepositoryAccessCheckResponse accessCheck;
        try
        {
            if (gitRequest.Operation == RepositoryOperation.WriteGit
                && HttpMethods.IsPost(context.Request.Method))
            {
                context.Request.EnableBuffering();
                await using var bodyCopy = new MemoryStream();
                await context.Request.Body
                    .CopyToAsync(bodyCopy, context.RequestAborted)
                    .ConfigureAwait(false);
                bodyCopy.Position = 0;
                context.Request.Body.Position = 0;

                var (_, refUpdates) = await GitReceivePackParser
                    .ReadPrefixAsync(bodyCopy, context.RequestAborted)
                    .ConfigureAwait(false);
                accessCheck = await _accessCheckClient
                    .CheckWithTokenAsync(
                        accessToken,
                        gitRequest.RepositoryPath,
                        gitRequest.Operation,
                        refUpdates,
                        packSizeBytes: bodyCopy.Length,
                        maxFileBytes: 0,
                        context.RequestAborted
                    )
                    .ConfigureAwait(false);
            }
            else
            {
                accessCheck = await _accessCheckClient
                    .CheckWithTokenAsync(
                        accessToken,
                        gitRequest.RepositoryPath,
                        gitRequest.Operation,
                        context.RequestAborted
                    )
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Repository access check failed for {RepositoryPath}", gitRequest.RepositoryPath);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (!accessCheck.Allowed)
        {
            context.Response.StatusCode = MapDeniedStatus(accessCheck.Reason);
            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                context.Response.Headers.WWWAuthenticate = WwwAuthenticateHeader;
            }

            return;
        }

        try
        {
            await _gitHttpProxyService
                .ProxyAsync(context, accessCheck, gitRequest, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git HTTP proxy failed for {RepositoryPath}", gitRequest.RepositoryPath);
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
        }
    }

    private static int MapDeniedStatus(string? reason)
    {
        if (reason is null)
        {
            return StatusCodes.Status403Forbidden;
        }

        if (reason.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("expired", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCodes.Status401Unauthorized;
        }

        return StatusCodes.Status403Forbidden;
    }
}
