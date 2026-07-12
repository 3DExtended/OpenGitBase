using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OpenGitBase.Cli.Auth;

internal static class LoopbackAuthHelpers
{
    public static bool IsValidCallback(string? expectedState, string? actualState, string? token) =>
        !string.IsNullOrWhiteSpace(token)
        && !string.IsNullOrWhiteSpace(actualState)
        && string.Equals(expectedState, actualState, StringComparison.Ordinal);

    public static int ReserveEphemeralPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static NameValueCollection ParseQueryString(string query)
    {
        var values = new NameValueCollection(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query))
        {
            return values;
        }

        var trimmed = query.StartsWith('?') ? query[1..] : query;
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0)
            {
                values[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
            values[key] = value;
        }

        return values;
    }

    public static async Task WriteResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string body)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "text/plain; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(body);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.OutputStream.Close();
    }
}
