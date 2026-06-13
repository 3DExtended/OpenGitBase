namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class OpenGenericStubHandler<T> : IQueryHandler<StubCountQuery, int>
{
    public Task<Option<int>> RunQueryAsync(
        StubCountQuery query,
        CancellationToken cancellationToken
    ) => Task.FromResult(Option.From(42));
}
