namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubCreateQuery : CreateQuery<StubModel, StubIdentifier, int, StubCreateQuery>
{
    public string? SelectorName { get; init; }
}
