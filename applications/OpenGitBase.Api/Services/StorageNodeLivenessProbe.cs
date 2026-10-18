namespace OpenGitBase.Api.Services;

/// <summary>
/// Sends one GET without a token to the node's internal server. It reads nothing: the node answers 401, and any
/// answer proves the node is up. Only a failed connection or no answer within the timeout counts as down.
/// </summary>
public sealed class StorageNodeLivenessProbe : IStorageNodeLivenessProbe
{
    /// <summary>Failover asks once per repository and cycle; a node that is gone must not hold the cycle.</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    public StorageNodeLivenessProbe(HttpClient httpClient)
        : this(httpClient, DefaultTimeout)
    {
    }

    internal StorageNodeLivenessProbe(HttpClient httpClient, TimeSpan timeout)
    {
        _httpClient = httpClient;
        _timeout = timeout;
    }

    public async Task<bool> AnswersAsync(string internalHost, int internalHttpPort, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            using var response = await _httpClient
                .GetAsync(
                    $"http://{internalHost}:{internalHttpPort}/",
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token
                )
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
            when (!cancellationToken.IsCancellationRequested
                && ex is HttpRequestException or OperationCanceledException)
        {
            return false;
        }
    }
}
