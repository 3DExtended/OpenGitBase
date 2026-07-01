namespace OpenGitBase.E2E.Core;

public interface ITestRunContext
{
    string RunSuffix { get; }

    BaselineNormalizer Normalizer { get; }

    Task ResetDatabaseAsync(CancellationToken cancellationToken = default);

    Task ClearCapturedEmailsAsync(CancellationToken cancellationToken = default);
}

public sealed class TestIsolation : ITestRunContext
{
    public TestIsolation(string? runSuffix = null)
    {
        RunSuffix = runSuffix ?? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        Normalizer = new BaselineNormalizer(RunSuffix);
    }

    public string RunSuffix { get; }

    public BaselineNormalizer Normalizer { get; }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = E2eEnvironment.ApiBaseUrl };
        try
        {
            await client.PostAsync("internal/e2e/reset-database", null, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Reset endpoint may not exist yet; tests rely on unique suffix isolation.
        }
    }

    public async Task ClearCapturedEmailsAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = E2eEnvironment.ApiBaseUrl };
        try
        {
            await client.PostAsync("internal/e2e/emails/clear", null, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Email clear endpoint optional.
        }
    }
}

public sealed class AuthenticatedClient
{
    public string Username { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public E2eApiClient Client { get; init; } = null!;
}

public interface IIdentityFixture
{
    Task SeedCoreRolesAsync(CancellationToken cancellationToken = default);

    AuthenticatedClient AsAdmin { get; }

    AuthenticatedClient AsWriter { get; }

    AuthenticatedClient AsOutsider { get; }

    E2eApiClient Anonymous { get; }
}

public sealed class IdentityFixture : IIdentityFixture
{
    private readonly ITestRunContext _context;
    private readonly IOperationTranscript _transcript;
    private readonly string _password = "Password123!";

    public IdentityFixture(ITestRunContext context, IOperationTranscript transcript)
    {
        _context = context;
        _transcript = transcript;
        Anonymous = new E2eApiClient(_transcript, _context.Normalizer);
    }

    public AuthenticatedClient AsAdmin { get; private set; } = null!;

    public AuthenticatedClient AsWriter { get; private set; } = null!;

    public AuthenticatedClient AsOutsider { get; private set; } = null!;

    public E2eApiClient Anonymous { get; }

    public async Task SeedCoreRolesAsync(CancellationToken cancellationToken = default)
    {
        _transcript.Describe("Seed core roles: admin, writer, outsider");
        AsAdmin = await CreateUserAsync($"e2e-admin-{_context.RunSuffix}", cancellationToken).ConfigureAwait(false);
        AsWriter = await CreateUserAsync($"e2e-writer-{_context.RunSuffix}", cancellationToken).ConfigureAwait(false);
        AsOutsider = await CreateUserAsync($"e2e-outsider-{_context.RunSuffix}", cancellationToken).ConfigureAwait(false);
    }

    private async Task<AuthenticatedClient> CreateUserAsync(string username, CancellationToken cancellationToken)
    {
        var anon = new E2eApiClient(_transcript, _context.Normalizer);
        await anon.PostAsync("/register/register", new
        {
            username,
            email = $"{username}@example.com",
            password = _password,
        }, cancellationToken).ConfigureAwait(false);

        var login = await anon.PostAsync("/signin/login", new { username, password = _password }, cancellationToken).ConfigureAwait(false);
        var token = E2eScenarioHelpers.ParseJwtToken(login);
        var client = new E2eApiClient(_transcript, _context.Normalizer, token);
        await client.PostAsync("/account/debug/verify-email", null, cancellationToken).ConfigureAwait(false);
        var userId = E2eScenarioHelpers.ExtractUserIdFromJwt(token);
        _context.Normalizer.RegisterToken("USER_ID", userId);
        return new AuthenticatedClient
        {
            Username = username,
            Token = token,
            UserId = userId,
            Client = client,
        };
    }
}
