using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OpenGitBase.Features.AccountSecurity.Tests.Testing;

/// <summary>Keeps every log entry the handlers write, with its event id and structured values.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLogEntry> _entries = new();

    public IReadOnlyList<RecordedLogEntry> Entries => _entries.ToList();

    public IReadOnlyList<RecordedLogEntry> Named(string eventName) =>
        Entries.Where(entry => entry.EventId.Name == eventName).ToList();

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(string category, ConcurrentQueue<RecordedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();
            entries.Enqueue(new RecordedLogEntry(category, logLevel, eventId, formatter(state, exception), properties));
        }
    }
}
