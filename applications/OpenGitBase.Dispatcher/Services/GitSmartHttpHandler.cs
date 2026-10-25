using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Options;

namespace OpenGitBase.Dispatcher.Services;

public sealed class GitSmartHttpHandler
{
    private const string WwwAuthenticateHeader = "Basic realm=\"OpenGitBase\"";

    private readonly GitSmartHttpPathParser _pathParser;
    private readonly RepositoryAccessCheckClient _accessCheckClient;
    private readonly GitHttpProxyService _gitHttpProxyService;
    private readonly DispatcherOptions _options;
    private readonly ILogger<GitSmartHttpHandler> _logger;
    private readonly LfsPathParser _lfsPathParser;
    private readonly LfsHandler _lfsHandler;

    public GitSmartHttpHandler(
        GitSmartHttpPathParser pathParser,
        RepositoryAccessCheckClient accessCheckClient,
        GitHttpProxyService gitHttpProxyService,
        IOptions<DispatcherOptions> options,
        ILogger<GitSmartHttpHandler> logger,
        LfsPathParser lfsPathParser,
        LfsHandler lfsHandler
    )
    {
        _pathParser = pathParser;
        _accessCheckClient = accessCheckClient;
        _gitHttpProxyService = gitHttpProxyService;
        _options = options.Value;
        _logger = logger;
        _lfsPathParser = lfsPathParser;
        _lfsHandler = lfsHandler;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (_lfsPathParser.TryParse(context.Request.Method, context.Request.Path.Value, out var lfsRequest))
        {
            await _lfsHandler.HandleAsync(context, lfsRequest).ConfigureAwait(false);
            return;
        }

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
                RaiseRequestBodyLimit(context);

                // The access check needs the ref updates and the pack size before anything reaches storage.
                // EnableBuffering spills to a temp file, so a large push is not held in memory.
                context.Request.EnableBuffering();
                var (_, refUpdates) = await GitReceivePackParser
                    .ReadPrefixAsync(context.Request.Body, context.RequestAborted)
                    .ConfigureAwait(false);
                await context.Request.Body
                    .CopyToAsync(Stream.Null, context.RequestAborted)
                    .ConfigureAwait(false);
                var packSizeBytes = context.Request.Body.Length;
                context.Request.Body.Position = 0;

                accessCheck = await _accessCheckClient
                    .CheckWithTokenAsync(
                        accessToken,
                        gitRequest.RepositoryPath,
                        gitRequest.Operation,
                        refUpdates,
                        packSizeBytes,
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
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            _logger.LogWarning(
                "Push to {RepositoryPath} exceeds the {MaxPushBytes}-byte limit",
                gitRequest.RepositoryPath,
                _options.MaxPushBytes
            );
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
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

    private void RaiseRequestBodyLimit(HttpContext context)
    {
        var bodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySize is { IsReadOnly: false })
        {
            bodySize.MaxRequestBodySize = _options.MaxPushBytes;
        }
    }
}
