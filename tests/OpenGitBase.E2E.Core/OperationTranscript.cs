namespace OpenGitBase.E2E.Core;

public enum WireEventKind
{
    HttpRequest,
    HttpResponse,
    GitCommand,
    GitOutput,
    EmailCaptured,
    ClusterAction,
    PlaywrightStep,
    Intent,
}

public sealed class WireEvent
{
    public WireEventKind Kind { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public int? StatusCode { get; init; }

    public string? Method { get; init; }

    public string? Url { get; init; }
}

public sealed class TranscriptEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public WireEventKind Kind { get; init; }

    public string Text { get; init; } = string.Empty;

    public string? Detail { get; init; }
}

public interface IOperationTranscript
{
    IReadOnlyList<TranscriptEntry> Entries { get; }

    void Describe(string humanIntent, object? context = null);

    void RecordWire(WireEvent evt);

    string SerializeNormalized(BaselineNormalizer normalizer);
}

public sealed class OperationTranscript : IOperationTranscript
{
    private readonly List<TranscriptEntry> _entries = [];

    public IReadOnlyList<TranscriptEntry> Entries => _entries;

    public void Clear() => _entries.Clear();

    public void Describe(string humanIntent, object? context = null)
    {
        var detail = context?.ToString();
        _entries.Add(new TranscriptEntry
        {
            Kind = WireEventKind.Intent,
            Text = humanIntent,
            Detail = detail,
        });
    }

    public void RecordWire(WireEvent evt)
    {
        _entries.Add(new TranscriptEntry
        {
            Kind = evt.Kind,
            Text = evt.Summary,
            Detail = evt.Detail ?? BuildDetail(evt),
        });
    }

    public string SerializeNormalized(BaselineNormalizer normalizer)
    {
        var lines = _entries.Select(e =>
            $"[{e.Kind}] {normalizer.Normalize(e.Text)}{(string.IsNullOrEmpty(e.Detail) ? string.Empty : $" | {normalizer.Normalize(e.Detail)}")}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildDetail(WireEvent evt)
    {
        if (evt.Kind is WireEventKind.HttpRequest or WireEventKind.HttpResponse)
        {
            return $"{evt.Method} {evt.Url} {(evt.StatusCode.HasValue ? evt.StatusCode.Value.ToString() : string.Empty)}".Trim();
        }

        return string.Empty;
    }
}
