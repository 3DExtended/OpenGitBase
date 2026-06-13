namespace OpenGitBase.Cqrs.Tests.Stubs;

internal abstract class AbstractStubHandler : IQueryHandler<StubStringQuery, string>
{
    public Task<Option<string>> RunQueryAsync(
        StubStringQuery query,
        CancellationToken cancellationToken
    ) => Task.FromResult(Option.From("abstract"));
}
