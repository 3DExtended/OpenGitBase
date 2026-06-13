namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubUpdateCommand : UpdateCommand<StubModel, StubIdentifier, int, StubUpdateCommand>;
