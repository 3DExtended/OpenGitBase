using OpenGitBase.Cli.Auth;

namespace OpenGitBase.Cli.Tests;

public sealed class LoopbackAuthServerIntegrationTests
{
    [Fact]
    public async Task Valid_callback_delivers_token()
    {
        using var server = new LoopbackAuthServer();
        var session = await server.StartAsync().ConfigureAwait(false);
        var waitTask = server.WaitForTokenAsync(TimeSpan.FromSeconds(5));
        var url =
            $"http://127.0.0.1:{session.Port}/callback?token=integration-jwt&state={Uri.EscapeDataString(session.State)}";

        using var client = new HttpClient();
        var response = await client.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var token = await waitTask.ConfigureAwait(false);
        Assert.Equal("integration-jwt", token);
        await server.StopAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Invalid_state_returns_bad_request()
    {
        using var server = new LoopbackAuthServer();
        var session = await server.StartAsync().ConfigureAwait(false);
        var url = $"http://127.0.0.1:{session.Port}/callback?token=jwt&state=wrong";

        using var client = new HttpClient();
        var response = await client.GetAsync(url).ConfigureAwait(false);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        await server.StopAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Duplicate_callback_returns_conflict()
    {
        using var server = new LoopbackAuthServer();
        var session = await server.StartAsync().ConfigureAwait(false);
        _ = server.WaitForTokenAsync(TimeSpan.FromSeconds(5));
        var url =
            $"http://127.0.0.1:{session.Port}/callback?token=first&state={Uri.EscapeDataString(session.State)}";

        using var client = new HttpClient();
        var first = await client.GetAsync(url).ConfigureAwait(false);
        first.EnsureSuccessStatusCode();
        var second = await client.GetAsync(url).ConfigureAwait(false);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, second.StatusCode);
        await server.StopAsync().ConfigureAwait(false);
    }
}
