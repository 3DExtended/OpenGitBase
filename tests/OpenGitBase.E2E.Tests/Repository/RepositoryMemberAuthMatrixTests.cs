using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Repository;

/// <summary>
/// Shared personas/repo for member matrix theory rows — avoids N× register storms that take down compose.
/// </summary>
public sealed class RepositoryMemberMatrixFixture : IAsyncLifetime
{
    public TestIsolation Context { get; } = new();

    public OperationTranscript Transcript { get; } = new();

    public AuthenticatedClient Owner { get; private set; } = null!;

    public AuthenticatedClient Reader { get; private set; } = null!;

    public AuthenticatedClient Writer { get; private set; } = null!;

    public AuthenticatedClient Outsider { get; private set; } = null!;

    public AuthenticatedClient Candidate { get; private set; } = null!;

    public string RepositoryId { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Context.ClearCapturedEmailsAsync().ConfigureAwait(false);
        var identity = new IdentityFixture(Context, Transcript);
        var repos = new RepositoryFixture(Transcript, Context.Normalizer);
        Owner = await identity.RegisterUserAsync($"rm-fx-owner-{Context.RunSuffix}").ConfigureAwait(false);
        Reader = await identity.RegisterUserAsync($"rm-fx-reader-{Context.RunSuffix}").ConfigureAwait(false);
        Writer = await identity.RegisterUserAsync($"rm-fx-writer-{Context.RunSuffix}").ConfigureAwait(false);
        Outsider = await identity.RegisterUserAsync($"rm-fx-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        Candidate = await identity.RegisterUserAsync($"rm-fx-candidate-{Context.RunSuffix}").ConfigureAwait(false);
        Slug = $"rm-fx-{Context.RunSuffix}";
        var repository = await repos.CreateAsync(Owner, Slug, "Member Matrix Shared", isPrivate: true).ConfigureAwait(false);
        RepositoryId = repository.RepositoryId;
        await repos.AddMemberAsync(Owner, RepositoryId, Reader.UserId, role: 1).ConfigureAwait(false);
        await repos.AddMemberAsync(Owner, RepositoryId, Writer.UserId, role: 2).ConfigureAwait(false);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[Collection("Compose")]
[Trait("Category", "RepositoryMember")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class RepositoryMemberAuthMatrixTests : AuthMatrixTheoryBase, IClassFixture<RepositoryMemberMatrixFixture>
{
    private readonly RepositoryMemberMatrixFixture _fixture;

    public RepositoryMemberAuthMatrixTests(RepositoryMemberMatrixFixture fixture)
    {
        _fixture = fixture;
    }

    public static IEnumerable<object[]> MemberMatrixCases() =>
        RepositoryMemberMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(MemberMatrixCases))]
    [Trait("Tag", "Smoke")]
    [Trait("Tag", "Regression")]
    public async Task RepositoryMemberAccessMatrix(AuthMatrixCase matrixCase)
    {
        // Fresh HttpClients per theory row — shared fixture clients can go stale across cases.
        var owner = CloneAuth(_fixture.Owner);
        var reader = CloneAuth(_fixture.Reader);
        var writer = CloneAuth(_fixture.Writer);
        var outsider = CloneAuth(_fixture.Outsider);

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{OWNER}}", owner.Username, StringComparison.Ordinal)
                .Replace("{{SLUG}}", _fixture.Slug, StringComparison.Ordinal)
                .Replace("{{REPO_ID}}", _fixture.RepositoryId, StringComparison.Ordinal)
                .Replace("{{READER_ID}}", reader.UserId, StringComparison.Ordinal)
                .Replace("{{WRITER_ID}}", writer.UserId, StringComparison.Ordinal)
                .Replace("{{CANDIDATE_ID}}", _fixture.Candidate.UserId, StringComparison.Ordinal)
                .Replace("{{OUTSIDER_ID}}", outsider.UserId, StringComparison.Ordinal),
            Body = ResolveBody(
                matrixCase.Body,
                _fixture.RepositoryId,
                reader.UserId,
                writer.UserId,
                _fixture.Candidate.UserId),
        };

        await RunMatrixCaseAsync(
            resolved,
            new AuthMatrixContext
            {
                Anonymous = new E2eApiClient(Transcript, Context.Normalizer),
                Owner = owner,
                Reader = reader,
                Writer = writer,
                Outsider = outsider,
            }).ConfigureAwait(false);
    }

    private AuthenticatedClient CloneAuth(AuthenticatedClient source) =>
        new()
        {
            Username = source.Username,
            UserId = source.UserId,
            Token = source.Token,
            Client = new E2eApiClient(Transcript, Context.Normalizer, source.Token),
        };

    private static object? ResolveBody(
        object? body,
        string repoId,
        string readerId,
        string writerId,
        string candidateId)
    {
        if (body is null)
        {
            return null;
        }

        var json = System.Text.Json.JsonSerializer.Serialize(body)
            .Replace("{{REPO_ID}}", repoId, StringComparison.Ordinal)
            .Replace("{{READER_ID}}", readerId, StringComparison.Ordinal)
            .Replace("{{WRITER_ID}}", writerId, StringComparison.Ordinal)
            .Replace("{{CANDIDATE_ID}}", candidateId, StringComparison.Ordinal);
        return System.Text.Json.JsonSerializer.Deserialize<object>(json);
    }
}

internal static class RepositoryMemberMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        // Prefer read-only + deny probes so a shared fixture stays stable across theory rows.
        var probes = new (HttpMethod Method, string Url, int Owner, int Writer, int Reader, int Outsider, int Anonymous, object? Body, string Intent)[]
        {
            (HttpMethod.Get, "/repository-member/{{REPO_ID}}", 200, 200, 200, 403, 401, null, "list members"),
            (HttpMethod.Post, "/repository-member", 500, 500, 500, 500, 401, MemberBody("{{REPO_ID}}", "00000000-0000-0000-0000-000000000000", 1), "add unknown user"),
            (HttpMethod.Put, "/repository-member/00000000-0000-0000-0000-000000000000", 400, 400, 400, 400, 401, UpdateBody("{{REPO_ID}}", 2), "update missing membership"),
            (HttpMethod.Delete, "/repository-member/00000000-0000-0000-0000-000000000000", 404, 404, 404, 404, 401, null, "delete missing membership"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/refs", 200, 200, 200, 403, 404, null, "private refs by role"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}", 200, 200, 200, 403, 500, null, "private repo metadata by role"),
            (HttpMethod.Get, "/repository/{{REPO_ID}}", 200, 200, 200, 403, 500, null, "get repository by id"),
            (HttpMethod.Get, "/repository/{{REPO_ID}}/usage", 200, 200, 200, 403, 500, null, "get repository usage"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/readme?refName=main", 404, 404, 404, 403, 404, null, "empty private readme by role"),
            (HttpMethod.Get, "/repository-member/{{REPO_ID}}", 200, 200, 200, 403, 401, null, "list members again"),
        };

        var actors = new[]
        {
            new { Actor = AuthMatrixActor.Owner, Label = "owner", Status = (Func<(int, int, int, int, int), int>)(s => s.Item1) },
            new { Actor = AuthMatrixActor.Writer, Label = "writer", Status = (Func<(int, int, int, int, int), int>)(s => s.Item2) },
            new { Actor = AuthMatrixActor.Reader, Label = "reader", Status = (Func<(int, int, int, int, int), int>)(s => s.Item3) },
            new { Actor = AuthMatrixActor.Outsider, Label = "outsider", Status = (Func<(int, int, int, int, int), int>)(s => s.Item4) },
            new { Actor = AuthMatrixActor.Anonymous, Label = "anonymous", Status = (Func<(int, int, int, int, int), int>)(s => s.Item5) },
        };

        var cases = new List<AuthMatrixCase>();
        var id = 1;
        foreach (var probe in probes)
        {
            var statuses = (probe.Owner, probe.Writer, probe.Reader, probe.Outsider, probe.Anonymous);
            foreach (var actor in actors)
            {
                cases.Add(Row(
                    $"E2E-F04-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    probe.Body,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-member-probe-{id}"));
                id++;
            }
        }

        return cases;
    }

    private static AuthMatrixCase Row(
        string id,
        AuthMatrixActor actor,
        HttpMethod method,
        string url,
        object? body,
        int status,
        string intent,
        string baseline) =>
        new(id, actor, method, url, body, status, intent, baseline);

    private static object MemberBody(string repoId, string userId, int role) => new
    {
        modelToCreate = new
        {
            repositoryId = new { value = repoId },
            userId = new { value = userId },
            role,
        },
    };

    private static object UpdateBody(string repoId, int role) => new
    {
        updatedModel = new
        {
            id = new { value = "00000000-0000-0000-0000-000000000000" },
            repositoryId = new { value = repoId },
            role,
        },
    };
}
