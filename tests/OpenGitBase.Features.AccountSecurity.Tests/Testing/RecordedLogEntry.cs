using Microsoft.Extensions.Logging;

namespace OpenGitBase.Features.AccountSecurity.Tests.Testing;

/// <param name="Properties">The structured values of the message template, by name.</param>
public sealed record RecordedLogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties
);
