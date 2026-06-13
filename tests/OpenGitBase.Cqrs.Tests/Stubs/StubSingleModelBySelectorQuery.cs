namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubSingleModelBySelectorQuery
    : SingleModelBySelectorQuery<StubModel, StubIdentifier, int, StubSingleModelBySelectorQuery>
{
    public string Name { get; init; } = string.Empty;
}
