namespace OpenGitBase.Cqrs;

/// <summary>
/// Base class for commands (mostly for convenience).
/// </summary>
public abstract class Command<TSelf> : IQuery<Unit, TSelf>
    where TSelf : IQuery<Unit, TSelf>;
