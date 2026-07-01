using System.Collections.Concurrent;
using System.Text.Json;

namespace OpenGitBase.Common.SendGrid;

public sealed class CapturingEmailStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ConcurrentBag<CapturedEmailMessage> _messages = [];
    private readonly string? _sharedPath;
    private readonly object _fileLock = new();

    public CapturingEmailStore()
    {
        _sharedPath = Environment.GetEnvironmentVariable("E2E__SharedEmailStorePath");
        if (!string.IsNullOrWhiteSpace(_sharedPath))
        {
            LoadFromFile();
        }
    }

    public void Add(CapturedEmailMessage message)
    {
        _messages.Add(message);
        PersistToFile();
    }

    public IReadOnlyList<CapturedEmailMessage> GetByRecipient(string? to = null)
    {
        if (!string.IsNullOrWhiteSpace(_sharedPath))
        {
            LoadFromFile();
        }

        var all = _messages.ToList();
        if (string.IsNullOrWhiteSpace(to))
        {
            return all;
        }

        return all.Where(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public void Clear()
    {
        while (!_messages.IsEmpty)
        {
            _messages.TryTake(out _);
        }

        PersistToFile();
    }

    private void LoadFromFile()
    {
        if (string.IsNullOrWhiteSpace(_sharedPath) || !File.Exists(_sharedPath))
        {
            return;
        }

        lock (_fileLock)
        {
            if (!File.Exists(_sharedPath))
            {
                return;
            }

            var json = File.ReadAllText(_sharedPath);
            var loaded = JsonSerializer.Deserialize<List<CapturedEmailMessage>>(json, JsonOptions) ?? [];
            while (!_messages.IsEmpty)
            {
                _messages.TryTake(out _);
            }

            foreach (var message in loaded)
            {
                _messages.Add(message);
            }
        }
    }

    private void PersistToFile()
    {
        if (string.IsNullOrWhiteSpace(_sharedPath))
        {
            return;
        }

        lock (_fileLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_sharedPath)!);
            var json = JsonSerializer.Serialize(_messages.ToList(), JsonOptions);
            File.WriteAllText(_sharedPath, json);
        }
    }
}
