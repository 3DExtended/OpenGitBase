using System.Net;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class IssueCommandTests
{
    [Fact]
    public async Task Issue_create_prints_number_and_url()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.Created,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"Bug","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var overrides = CreateOverrides(handler, "https://forge.example.com");

        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "create", "--title", "Bug", "--body", "Details",
            ],
            output,
            error,
            overrides);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("#42", text, StringComparison.Ordinal);
        Assert.Contains("https://forge.example.com/acme/demo/discussions/42", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_close_default_calls_resolve_endpoint()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"Bug","status":2,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var overrides = CreateOverrides(handler, "https://forge.example.com");

        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "issue", "-R", "acme/demo", "close", "42"],
            output,
            error,
            overrides);

        Assert.Equal(0, exitCode);
        Assert.Contains("/discussions/42/resolve", handler.Requests.Single().RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("Resolved", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_close_dismissed_calls_dismiss_endpoint()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"Bug","status":3,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var overrides = CreateOverrides(handler, "https://forge.example.com");

        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "close", "42", "--reason", "dismissed",
            ],
            output,
            error,
            overrides);

        Assert.Equal(0, exitCode);
        Assert.Contains("/discussions/42/dismiss", handler.Requests.Single().RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_repo_context_exits_non_zero()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var handler = new StubHttpMessageHandler();
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var overrides = new CliServiceOverrides
        {
            CredentialStore = CreateCredentialStore("https://forge.example.com"),
            HttpClient = new HttpClient(handler),
            WorkingDirectory = tempDir.FullName,
        };

        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "issue", "list"],
            output,
            error,
            overrides);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("repository context", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Json_auth_status_emits_structured_output()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var overrides = new CliServiceOverrides
        {
            CredentialStore = CreateCredentialStore("https://forge.example.com"),
        };

        var exitCode = await CliApp
            .RunAsync(["--hostname", "https://forge.example.com", "--json", "auth", "status"], output, error, overrides);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"loggedIn\":true", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"username\":\"alice\"", output.ToString(), StringComparison.Ordinal);
    }

    private static CliServiceOverrides CreateOverrides(StubHttpMessageHandler handler, string host) =>
        new()
        {
            CredentialStore = CreateCredentialStore(host),
            HttpClient = new HttpClient(handler),
        };

    private static InMemoryCredentialStore CreateCredentialStore(string host)
    {
        var credentialStore = new InMemoryCredentialStore();
        credentialStore.SaveToken(host, AuthCommandTestsHelpers.CreateJwt("alice"));
        return credentialStore;
    }
}
