using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OpenGitBase.Api.Tests.Services.Lfs;

/// <summary>Keeps every formatted log message, so tests can check what an operator would read.</summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, string Message)> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => _entries.Enqueue((logLevel, formatter(state, exception)));
}
