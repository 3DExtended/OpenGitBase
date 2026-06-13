namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubSingleModelQuery
    : SingleModelQuery<StubModel, StubIdentifier, int, StubSingleModelQuery>;
