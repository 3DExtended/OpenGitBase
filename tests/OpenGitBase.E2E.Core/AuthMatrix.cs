namespace OpenGitBase.E2E.Core;

public enum AuthMatrixActor
{
    Anonymous,
    Outsider,
    Reader,
    Writer,
    Owner,
    Admin,
}

public sealed record AuthMatrixCase(
    string CatalogId,
    AuthMatrixActor Actor,
    HttpMethod Method,
    string RelativeUrl,
    object? Body,
    int ExpectedStatus,
    string Intent,
    string BaselineKey,
    bool NotApplicable = false,
    string? SkipReason = null);

public sealed class AuthMatrixContext
{
    public E2eApiClient Anonymous { get; init; } = null!;

    public AuthenticatedClient? Outsider { get; init; }

    public AuthenticatedClient? Reader { get; init; }

    public AuthenticatedClient? Writer { get; init; }

    public AuthenticatedClient? Owner { get; init; }

    public AuthenticatedClient? Admin { get; init; }

    public E2eApiClient ResolveClient(AuthMatrixActor actor) =>
        actor switch
        {
            AuthMatrixActor.Anonymous => Anonymous,
            AuthMatrixActor.Outsider => Require(Outsider, actor).Client,
            AuthMatrixActor.Reader => Require(Reader, actor).Client,
            AuthMatrixActor.Writer => Require(Writer, actor).Client,
            AuthMatrixActor.Owner => Require(Owner, actor).Client,
            AuthMatrixActor.Admin => Require(Admin, actor).Client,
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, null),
        };

    private static AuthenticatedClient Require(AuthenticatedClient? client, AuthMatrixActor actor) =>
        client ?? throw new InvalidOperationException($"Auth matrix actor {actor} was not seeded.");
}

public static class AuthMatrixRunner
{
    public static async Task<HttpCapture> ExecuteAsync(
        AuthMatrixCase matrixCase,
        AuthMatrixContext context,
        CancellationToken cancellationToken = default)
    {
        var client = context.ResolveClient(matrixCase.Actor);
        return await client.SendAsync(matrixCase.Method, matrixCase.RelativeUrl, matrixCase.Body, cancellationToken)
            .ConfigureAwait(false);
    }
}
