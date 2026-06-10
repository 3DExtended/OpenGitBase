namespace OpenGitBase.Cqrs.EfCore;

public interface IIdentifiableEntity<TIdentifierValue>
{
    TIdentifierValue Id { get; }
}
