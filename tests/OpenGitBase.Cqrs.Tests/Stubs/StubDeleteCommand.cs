namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubDeleteCommand : DeleteCommand<StubModel, StubIdentifier, int, StubDeleteCommand>;
