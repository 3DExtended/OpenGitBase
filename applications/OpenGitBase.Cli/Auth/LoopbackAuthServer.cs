using System.Net;
using System.Security.Cryptography;

namespace OpenGitBase.Cli.Auth;

public sealed class LoopbackAuthServer : ILoopbackAuthServer, IDisposable
{
    private readonly HttpListener _listener = new();
    private LoopbackAuthSession? _session;
    private TaskCompletionSource<string>? _tokenSource;
    private CancellationTokenRegistration _registration;
    private bool _disposed;

    public async Task<LoopbackAuthSession> StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        _tokenSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var port = LoopbackAuthHelpers.ReserveEphemeralPort();
        _listener.Prefixes.Clear();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();

        _session = new LoopbackAuthSession { Port = port, State = state };

        _ = Task.Run(() => ListenAsync(cancellationToken), cancellationToken);
        await Task.CompletedTask.ConfigureAwait(false);
        return _session;
    }

    public Task<string> WaitForTokenAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_tokenSource is null)
        {
            throw new InvalidOperationException("Loopback auth server has not been started.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        _registration = timeoutCts.Token.Register(() => _tokenSource.TrySetCanceled(timeoutCts.Token));
        return _tokenSource.Task;
    }

    public Task StopAsync()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registration.Dispose();
        _listener.Close();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (_listener.IsListening && !cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (!_listener.IsListening || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await HandleRequestAsync(context).ConfigureAwait(false);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var response = context.Response;
        var request = context.Request;
        var path = request.Url?.AbsolutePath ?? string.Empty;

        if (!string.Equals(path, _session?.CallbackPath, StringComparison.OrdinalIgnoreCase))
        {
            await LoopbackAuthHelpers.WriteResponseAsync(response, HttpStatusCode.NotFound, "Not found").ConfigureAwait(false);
            return;
        }

        var query = LoopbackAuthHelpers.ParseQueryString(request.Url?.Query ?? string.Empty);
        var token = query["token"];
        var state = query["state"];

        if (!LoopbackAuthHelpers.IsValidCallback(_session?.State, state, token))
        {
            await LoopbackAuthHelpers.WriteResponseAsync(response, HttpStatusCode.BadRequest, "Invalid callback.").ConfigureAwait(false);
            return;
        }

        if (!_tokenSource!.TrySetResult(token!))
        {
            await LoopbackAuthHelpers.WriteResponseAsync(response, HttpStatusCode.Conflict, "Callback already received.").ConfigureAwait(false);
            return;
        }

        await LoopbackAuthHelpers.WriteResponseAsync(
            response,
            HttpStatusCode.OK,
            "Login successful. You can close this window and return to the terminal.")
            .ConfigureAwait(false);
    }
}
