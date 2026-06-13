namespace OpenGitBase.Cqrs.Tests.Stubs;

public class StubModel : ModelBase<StubIdentifier, int>
{
    public string Name { get; set; } = string.Empty;
}
