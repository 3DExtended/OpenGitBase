using Microsoft.AspNetCore.Http;
using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

public sealed class GitHttpProxyService
{
    private static readonly string[] ForwardedRequestHeaders =
    [
        "Content-Type",
        "Content-Length",
        "Git-Protocol",
        "Accept",
    ];

    private readonly HttpClient _httpClient;

    public GitHttpProxyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public static string BuildStorageRelativePath(
        string physicalPath,
        GitSmartHttpRequest gitRequest,
        QueryString query
    )
    {
        var repoName = Path.GetFileName(physicalPath.TrimEnd('/'));
        if (string.IsNullOrWhiteSpace(repoName))
        {
            throw new InvalidOperationException("Physical path is missing a repository segment.");
        }

        var relativePath = $"/{repoName}/{gitRequest.GitSuffix}";
        if (gitRequest.GitSuffix.Equals("info/refs", StringComparison.OrdinalIgnoreCase)
            && query.HasValue)
        {
            relativePath += query.Value!;
        }

        return relativePath;
    }

    public async Task ProxyAsync(
        HttpContext context,
        RepositoryAccessCheckResponse accessCheck,
        GitSmartHttpRequest gitRequest,
        CancellationToken cancellationToken
    )
    {
        if (
            string.IsNullOrWhiteSpace(accessCheck.PhysicalPath)
            || string.IsNullOrWhiteSpace(accessCheck.StorageNodeInternalHost)
            || accessCheck.StorageNodeInternalGitHttpPort is null
        )
        {
            throw new InvalidOperationException("Access check is missing git HTTP routing fields.");
        }

        var relativePath = BuildStorageRelativePath(
            accessCheck.PhysicalPath,
            gitRequest,
            context.Request.QueryString
        );
        var storageUri = new Uri(
            $"http://{accessCheck.StorageNodeInternalHost}:{accessCheck.StorageNodeInternalGitHttpPort.Value}{relativePath}"
        );

        using var request = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            storageUri
        );

        if (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method))
        {
            request.Content = new StreamContent(context.Request.Body);
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
            {
                request.Content.Headers.TryAddWithoutValidation(
                    "Content-Type",
                    context.Request.ContentType
                );
            }
        }

        foreach (var headerName in ForwardedRequestHeaders)
        {
            if (context.Request.Headers.TryGetValue(headerName, out var values))
            {
                request.Headers.TryAddWithoutValidation(headerName, values.ToArray());
            }
        }

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        context.Response.StatusCode = (int)response.StatusCode;

        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.Headers.Remove("transfer-encoding");
        await response
            .Content.CopyToAsync(context.Response.Body, cancellationToken)
            .ConfigureAwait(false);
    }
}
