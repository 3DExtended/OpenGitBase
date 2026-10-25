using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace OpenGitBase.Dispatcher.Tests.RealGit;

/// <summary>
/// Storage's git HTTP endpoint: git-http-backend as CGI, fed the way nginx's FastCGI pass feeds it
/// (body read in full, CONTENT_TYPE/CONTENT_LENGTH, every other request header as HTTP_*).
/// </summary>
internal sealed class GitHttpBackendHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private GitHttpBackendHost(WebApplication app, int port)
    {
        _app = app;
        Port = port;
    }

    public int Port { get; }

    public static async Task<GitHttpBackendHost> StartAsync(string projectRoot, string home)
    {
        var app = TestKestrel.Build();
        app.Run(context => RunCgiAsync(context, projectRoot, home));
        return new GitHttpBackendHost(app, await TestKestrel.StartAsync(app));
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private static async Task RunCgiAsync(HttpContext context, string projectRoot, string home)
    {
        using var body = new MemoryStream();
        await context.Request.Body.CopyToAsync(body, context.RequestAborted);

        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("http-backend");
        var environment = startInfo.Environment;
        GitCli.IsolateGitEnvironment(environment, home);
        environment["GATEWAY_INTERFACE"] = "CGI/1.1";
        environment["SERVER_PROTOCOL"] = "HTTP/1.1";
        environment["REQUEST_METHOD"] = context.Request.Method;
        environment["PATH_INFO"] = context.Request.Path.Value;
        environment["QUERY_STRING"] = context.Request.QueryString.HasValue
            ? context.Request.QueryString.Value![1..]
            : string.Empty;
        environment["REMOTE_ADDR"] = "127.0.0.1";
        environment["REMOTE_USER"] = "git";
        environment["GIT_PROJECT_ROOT"] = projectRoot;
        environment["GIT_HTTP_EXPORT_ALL"] = string.Empty;
        if (HttpMethods.IsPost(context.Request.Method))
        {
            environment["CONTENT_LENGTH"] = body.Length.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrEmpty(context.Request.ContentType))
        {
            environment["CONTENT_TYPE"] = context.Request.ContentType;
        }

        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            environment["HTTP_" + header.Key.ToUpperInvariant().Replace('-', '_')] = header.Value.ToString();
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("git http-backend did not start");
        var output = new MemoryStream();
        var readOutput = process.StandardOutput.BaseStream.CopyToAsync(output);
        var readErrors = process.StandardError.ReadToEndAsync();
        body.Position = 0;
        await body.CopyToAsync(process.StandardInput.BaseStream);
        process.StandardInput.Close();
        await readOutput;
        await readErrors;
        await process.WaitForExitAsync();

        await WriteCgiResponseAsync(context, output.ToArray());
    }

    private static async Task WriteCgiResponseAsync(HttpContext context, byte[] output)
    {
        var headerEnd = IndexOf(output, "\r\n\r\n"u8, out var separatorLength);
        if (headerEnd < 0)
        {
            headerEnd = IndexOf(output, "\n\n"u8, out separatorLength);
        }

        if (headerEnd < 0)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        var headers = Encoding.ASCII.GetString(output, 0, headerEnd)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in headers)
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var name = line[..colon];
            var value = line[(colon + 1)..].Trim();
            if (name.Equals("Status", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = int.Parse(value[..3], CultureInfo.InvariantCulture);
            }
            else
            {
                context.Response.Headers.Append(name, value);
            }
        }

        var start = headerEnd + separatorLength;
        await context.Response.Body.WriteAsync(output.AsMemory(start, output.Length - start));
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle, out int length)
    {
        length = needle.Length;
        return haystack.AsSpan().IndexOf(needle);
    }
}
