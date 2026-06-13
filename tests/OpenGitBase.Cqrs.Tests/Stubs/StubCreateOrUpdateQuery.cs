namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubCreateOrUpdateQuery
    : CreateOrUpdateIfExistingQuery<StubModel, StubIdentifier, int, StubCreateOrUpdateQuery>
{
    public string MatchName { get; init; } = string.Empty;
}
