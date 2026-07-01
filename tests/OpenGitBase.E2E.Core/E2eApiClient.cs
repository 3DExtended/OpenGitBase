using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace OpenGitBase.E2E.Core;

public sealed class HttpCapture
{
    public int StatusCode { get; init; }

    public string Body { get; init; } = string.Empty;

    public string Method { get; init; } = "GET";

    public string Url { get; init; } = string.Empty;
}

public sealed class E2eApiClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly IOperationTranscript _transcript;
    private readonly BaselineNormalizer _normalizer;

    public E2eApiClient(IOperationTranscript transcript, BaselineNormalizer normalizer, string? bearerToken = null)
    {
        _transcript = transcript;
        _normalizer = normalizer;
        _client = new HttpClient { BaseAddress = E2eEnvironment.ApiBaseUrl };
        if (!string.IsNullOrEmpty(bearerToken))
        {
            SetBearerToken(bearerToken);
        }
    }

    public HttpClient RawClient => _client;

    public void SetBearerToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim('"'));
    }

    public void ClearAuth()
    {
        _client.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<HttpCapture> SendAsync(
        HttpMethod method,
        string relativeUrl,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedRelative = ToRelativeUri(relativeUrl);
        var url = relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativeUrl
            : new Uri(_client.BaseAddress!, resolvedRelative).ToString();

        _transcript.RecordWire(new WireEvent
        {
            Kind = WireEventKind.HttpRequest,
            Summary = $"{method} {url}",
            Method = method.Method,
            Url = url,
        });

        using var request = new HttpRequestMessage(method, resolvedRelative);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _transcript.RecordWire(new WireEvent
        {
            Kind = WireEventKind.HttpResponse,
            Summary = $"{(int)response.StatusCode} {response.ReasonPhrase}",
            Method = method.Method,
            Url = url,
            StatusCode = (int)response.StatusCode,
            Detail = _normalizer.Normalize(responseBody),
        });

        return new HttpCapture
        {
            StatusCode = (int)response.StatusCode,
            Body = responseBody,
            Method = method.Method,
            Url = url,
        };
    }

    public async Task<HttpCapture> GetAsync(string relativeUrl, CancellationToken cancellationToken = default) =>
        await SendAsync(HttpMethod.Get, relativeUrl, null, cancellationToken).ConfigureAwait(false);

    public async Task<HttpCapture> PostAsync(string relativeUrl, object? body = null, CancellationToken cancellationToken = default) =>
        await SendAsync(HttpMethod.Post, relativeUrl, body, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<CapturedEmail>> GetCapturedEmailsAsync(string to, CancellationToken cancellationToken = default)
    {
        var capture = await GetAsync($"internal/e2e/emails?to={Uri.EscapeDataString(to)}", cancellationToken).ConfigureAwait(false);
        if (capture.StatusCode != 200)
        {
            return Array.Empty<CapturedEmail>();
        }

        var emails = JsonSerializer.Deserialize<List<CapturedEmail>>(capture.Body, JsonOptions)
            ?? new List<CapturedEmail>();
        foreach (var email in emails)
        {
            _transcript.RecordWire(new WireEvent
            {
                Kind = WireEventKind.EmailCaptured,
                Summary = $"Email captured to {email.To}: {email.Subject}",
                Detail = _normalizer.Normalize(email.HtmlBody),
            });
        }

        return emails;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private static string ToRelativeUri(string relativeUrl) =>
        relativeUrl.StartsWith('/') ? relativeUrl.TrimStart('/') : relativeUrl;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}

public sealed class CapturedEmail
{
    public string To { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string HtmlBody { get; init; } = string.Empty;

    public DateTimeOffset SentAt { get; init; }
}
