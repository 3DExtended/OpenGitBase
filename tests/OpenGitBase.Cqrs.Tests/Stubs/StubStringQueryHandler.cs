namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubStringQueryHandler : IQueryHandler<StubStringQuery, string>
{
    public const string Result = "handled";

    public Task<Option<string>> RunQueryAsync(
        StubStringQuery query,
        CancellationToken cancellationToken
    ) => Task.FromResult(Option.From(Result));
}
