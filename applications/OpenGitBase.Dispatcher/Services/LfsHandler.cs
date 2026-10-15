using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

/// <summary>
/// Git LFS over HTTPS. The batch and verify calls go to the API, which owns every decision. Object bytes are
/// streamed between the client and storage here, after the API accepted the action token; nothing is buffered,
/// and an upload may not exceed the size the token was issued for.
/// </summary>
public sealed class LfsHandler
{
    public const string LfsContentType = "application/vnd.git-lfs+json";
    private const string WwwAuthenticateHeader = "Basic realm=\"OpenGitBase\"";

    // Batch and verify bodies are small JSON documents; a large body here is not a real LFS client.
    private const long MaxJsonRequestBytes = 4 * 1024 * 1024;
    private static readonly byte[] NoLocks = Encoding.UTF8.GetBytes("""{"ours":[],"theirs":[]}""");

    private readonly LfsApiClient _api;
    private readonly LfsObjectTransferService _transfers;
    private readonly ILogger<LfsHandler> _logger;

    public LfsHandler(LfsApiClient api, LfsObjectTransferService transfers, ILogger<LfsHandler> logger)
    {
        _api = api;
        _transfers = transfers;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context, LfsHttpRequest request)
    {
        try
        {
            switch (request.Kind)
            {
                case LfsRequestKind.Batch:
                    await HandleBatchAsync(context, request).ConfigureAwait(false);
                    break;
                case LfsRequestKind.Upload:
                    await HandleUploadAsync(context, request).ConfigureAwait(false);
                    break;
                case LfsRequestKind.Download:
                    await HandleDownloadAsync(context, request).ConfigureAwait(false);
                    break;
                case LfsRequestKind.Verify:
                    await HandleVerifyAsync(context, request).ConfigureAwait(false);
                    break;
                case LfsRequestKind.LocksVerify:
                    // Locking is not offered. Answering "no locks" keeps git push from printing a hint.
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = LfsContentType;
                    await context.Response.Body.WriteAsync(NoLocks, context.RequestAborted).ConfigureAwait(false);
                    break;
                default:
                    await WriteErrorAsync(context, StatusCodes.Status404NotFound, "Not found.").ConfigureAwait(false);
                    break;
            }
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            await WriteErrorAsync(context, StatusCodes.Status413PayloadTooLarge, "Request body too large.")
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException && !context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "LFS {Kind} for {RepositoryPath} failed", request.Kind, request.RepositoryPath);
            if (!context.Response.HasStarted)
            {
                await WriteErrorAsync(context, StatusCodes.Status502BadGateway, "Storage is unreachable; try again.")
                    .ConfigureAwait(false);
            }
        }
    }

    private static string BuildLfsBaseUrl(HttpRequest request, string repositoryPath)
    {
        var scheme = FirstHeaderValue(request, "X-Forwarded-Proto") ?? request.Scheme;
        var host = FirstHeaderValue(request, "X-Forwarded-Host") ?? request.Host.Value;
        return $"{scheme}://{host}/{repositoryPath}.git/info/lfs";
    }

    private static string? FirstHeaderValue(HttpRequest request, string name)
    {
        var value = request.Headers[name].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var first = value.Split(',')[0].Trim();
        return first.Length == 0 ? null : first;
    }

    private static bool TryReadActionToken(HttpRequest request, out string token)
    {
        var header = request.Headers.Authorization.ToString();
        token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : string.Empty;
        return token.Length > 0;
    }

    private static void LimitRequestBody(HttpContext context, long maxBytes)
    {
        var bodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySize is { IsReadOnly: false })
        {
            bodySize.MaxRequestBodySize = maxBytes;
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpContext context)
        where T : class
    {
        try
        {
            return await JsonSerializer
                .DeserializeAsync<T>(context.Request.Body, JsonSerializerOptions.Web, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task RelayAsync(HttpContext context, LfsApiAnswer answer)
    {
        context.Response.StatusCode = answer.StatusCode;
        context.Response.ContentType = LfsContentType;
        await context.Response.WriteAsync(answer.Body, context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = LfsContentType;
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["message"] = message,
            ["request_id"] = context.TraceIdentifier,
        });
        await context.Response.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
    }

    private async Task HandleBatchAsync(HttpContext context, LfsHttpRequest request)
    {
        LimitRequestBody(context, MaxJsonRequestBytes);
        var batch = await ReadJsonAsync<LfsClientBatchRequest>(context).ConfigureAwait(false);
        if (batch is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status422UnprocessableEntity, "Invalid batch request.")
                .ConfigureAwait(false);
            return;
        }

        BasicAuthTokenReader.TryReadAccessToken(context.Request, out var accessToken);
        var answer = await _api
            .BatchAsync(
                new LfsBatchForwardRequest
                {
                    AccessToken = accessToken,
                    RepositoryPath = request.RepositoryPath,
                    Operation = batch.Operation,
                    Transfers = batch.Transfers,
                    HashAlgo = batch.HashAlgo,
                    Objects = batch.Objects,
                    LfsBaseUrl = BuildLfsBaseUrl(context.Request, request.RepositoryPath),
                },
                context.RequestAborted
            )
            .ConfigureAwait(false);
        if (answer.StatusCode == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = WwwAuthenticateHeader;
        }

        await RelayAsync(context, answer).ConfigureAwait(false);
    }

    private async Task HandleUploadAsync(HttpContext context, LfsHttpRequest request)
    {
        var check = await CheckTransferAsync(context, request, "upload").ConfigureAwait(false);
        if (check is null)
        {
            return;
        }

        if (context.Request.ContentLength is not { } contentLength)
        {
            await WriteErrorAsync(context, StatusCodes.Status411LengthRequired, "Content-Length is required.")
                .ConfigureAwait(false);
            return;
        }

        if (contentLength != check.Size)
        {
            await WriteErrorAsync(
                    context,
                    StatusCodes.Status422UnprocessableEntity,
                    $"Upload is {contentLength} bytes; the object is {check.Size} bytes."
                )
                .ConfigureAwait(false);
            return;
        }

        LimitRequestBody(context, check.Size);
        using var response = await _transfers
            .UploadAsync(check.Targets[0], check.RepositoryId, check.Oid, context.Request.Body, check.Size, context.RequestAborted)
            .ConfigureAwait(false);
        switch ((int)response.StatusCode)
        {
            case StatusCodes.Status200OK or StatusCodes.Status201Created:
                context.Response.StatusCode = StatusCodes.Status200OK;
                break;
            case StatusCodes.Status422UnprocessableEntity:
                await WriteErrorAsync(
                        context,
                        StatusCodes.Status422UnprocessableEntity,
                        "Uploaded content does not match the object id."
                    )
                    .ConfigureAwait(false);
                break;
            default:
                _logger.LogWarning(
                    "Storage {Host} answered {Status} to LFS upload of {Oid}",
                    check.Targets[0].InternalHost,
                    (int)response.StatusCode,
                    check.Oid
                );
                await WriteErrorAsync(context, StatusCodes.Status502BadGateway, "Storage refused the object; try again.")
                    .ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleDownloadAsync(HttpContext context, LfsHttpRequest request)
    {
        var check = await CheckTransferAsync(context, request, "download").ConfigureAwait(false);
        if (check is null)
        {
            return;
        }

        foreach (var target in check.Targets)
        {
            HttpResponseMessage response;
            try
            {
                response = await _transfers
                    .OpenDownloadAsync(target, check.RepositoryId, check.Oid, context.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "LFS download of {Oid} from {Host} failed", check.Oid, target.InternalHost);
                continue;
            }

            using (response)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogInformation(
                        "Storage {Host} answered {Status} for LFS object {Oid}",
                        target.InternalHost,
                        (int)response.StatusCode,
                        check.Oid
                    );
                    continue;
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/octet-stream";
                context.Response.ContentLength = response.Content.Headers.ContentLength ?? check.Size;
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
                return;
            }
        }

        await WriteErrorAsync(context, StatusCodes.Status404NotFound, "Object not found on storage.")
            .ConfigureAwait(false);
    }

    private async Task HandleVerifyAsync(HttpContext context, LfsHttpRequest request)
    {
        if (!TryReadActionToken(context.Request, out var token))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "LFS action token required.")
                .ConfigureAwait(false);
            return;
        }

        LimitRequestBody(context, MaxJsonRequestBytes);
        var item = await ReadJsonAsync<LfsObjectSpec>(context).ConfigureAwait(false);
        if (item is null || !string.Equals(item.Oid, request.Oid, StringComparison.Ordinal))
        {
            await WriteErrorAsync(context, StatusCodes.Status422UnprocessableEntity, "Verify body does not name this object.")
                .ConfigureAwait(false);
            return;
        }

        var answer = await _api
            .VerifyAsync(
                new LfsVerifyForwardRequest
                {
                    Token = token,
                    Oid = item.Oid,
                    Size = item.Size,
                    RepositoryPath = request.RepositoryPath,
                },
                context.RequestAborted
            )
            .ConfigureAwait(false);
        await RelayAsync(context, answer).ConfigureAwait(false);
    }

    /// <summary>Asks the API whether the transfer may start. Writes the refusal and returns null if not.</summary>
    private async Task<LfsTransferCheckResult?> CheckTransferAsync(
        HttpContext context,
        LfsHttpRequest request,
        string operation
    )
    {
        if (!TryReadActionToken(context.Request, out var token))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "LFS action token required.")
                .ConfigureAwait(false);
            return null;
        }

        var answer = await _api
            .CheckTransferAsync(
                new LfsTransferCheckForwardRequest
                {
                    Token = token,
                    Oid = request.Oid!,
                    Operation = operation,
                    RepositoryPath = request.RepositoryPath,
                },
                context.RequestAborted
            )
            .ConfigureAwait(false);
        if (answer.StatusCode != StatusCodes.Status200OK)
        {
            await RelayAsync(context, answer).ConfigureAwait(false);
            return null;
        }

        var check = JsonSerializer.Deserialize<LfsTransferCheckResult>(answer.Body, JsonSerializerOptions.Web);
        if (check is null || check.Targets.Count == 0)
        {
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "Repository storage is unavailable.")
                .ConfigureAwait(false);
            return null;
        }

        return check;
    }
}
