namespace OpenGitBase.Cqrs.Tests.Stubs;

public class StubEntity : IIdentifiableEntity<int>
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
