using System.Text.Json;

namespace OpenGitBase.E2E.Core;

public sealed class BaselineDiff
{
    public string Path { get; init; } = string.Empty;

    public string Expected { get; init; } = string.Empty;

    public string Actual { get; init; } = string.Empty;

    public string Severity { get; init; } = "Error";
}

public interface IBaselineContext
{
    Task CaptureApiAsync(string stepId, HttpCapture capture, CancellationToken cancellationToken = default);

    Task CapturePageAsync(string stepId, string normalizedHtml, CancellationToken cancellationToken = default);

    Task CaptureGitStateAsync(string stepId, GitStateSnapshot state, CancellationToken cancellationToken = default);

    Task CaptureSideChannelAsync(string stepId, string channel, object payload, CancellationToken cancellationToken = default);

    Task AssertMatchesCommittedAsync(CancellationToken cancellationToken = default);

    Task UpdateCommittedAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<BaselineDiff> Diffs { get; }
}

public sealed class BaselineManager : IBaselineContext
{
    private readonly string _baselineRelativePath;
    private readonly BaselineNormalizer _normalizer;
    private readonly IOperationTranscript _transcript;
    private readonly bool _updateMode;
    private readonly Dictionary<string, string> _capturedFiles = new(StringComparer.Ordinal);
    private readonly List<BaselineDiff> _diffs = [];

    public BaselineManager(
        string baselineRelativePath,
        BaselineNormalizer normalizer,
        IOperationTranscript transcript,
        bool updateMode)
    {
        _baselineRelativePath = baselineRelativePath;
        _normalizer = normalizer;
        _transcript = transcript;
        _updateMode = updateMode;
    }

    public IReadOnlyList<BaselineDiff> Diffs => _diffs;

    public bool HasCommittedBundle()
    {
        var baselineRoot = Path.Combine(E2eEnvironment.BaselinesRoot, _baselineRelativePath);
        return Directory.Exists(baselineRoot)
            && Directory.EnumerateFiles(baselineRoot, "*", SearchOption.AllDirectories).Any();
    }

    public Task CaptureApiAsync(string stepId, HttpCapture capture, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            capture.StatusCode,
            Body = _normalizer.Normalize(capture.Body),
            capture.Method,
            Url = _normalizer.Normalize(capture.Url),
        }, JsonOptions);
        _capturedFiles[$"api/{stepId}.json"] = payload;
        return Task.CompletedTask;
    }

    public Task CapturePageAsync(string stepId, string normalizedHtml, CancellationToken cancellationToken = default)
    {
        _capturedFiles[$"pages/{stepId}.html"] = _normalizer.Normalize(normalizedHtml);
        return Task.CompletedTask;
    }

    public Task CaptureGitStateAsync(string stepId, GitStateSnapshot state, CancellationToken cancellationToken = default)
    {
        _capturedFiles[$"git/{stepId}.txt"] = _normalizer.Normalize(state.ToSummary());
        return Task.CompletedTask;
    }

    public Task CaptureSideChannelAsync(string stepId, string channel, object payload, CancellationToken cancellationToken = default)
    {
        var json = _normalizer.Normalize(JsonSerializer.Serialize(payload, JsonOptions));
        _capturedFiles[$"side-channel/{channel}/{stepId}.json"] = json;
        return Task.CompletedTask;
    }

    public async Task AssertMatchesCommittedAsync(CancellationToken cancellationToken = default)
    {
        await FinalizeOperationsAsync(cancellationToken).ConfigureAwait(false);
        var baselineRoot = Path.Combine(E2eEnvironment.BaselinesRoot, _baselineRelativePath);
        foreach (var (relativePath, actual) in _capturedFiles)
        {
            var committedPath = Path.Combine(baselineRoot, relativePath);
            if (!File.Exists(committedPath))
            {
                _diffs.Add(new BaselineDiff
                {
                    Path = relativePath,
                    Expected = "(missing baseline)",
                    Actual = actual,
                });
                continue;
            }

            var expected = await File.ReadAllTextAsync(committedPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(expected.Trim(), actual.Trim(), StringComparison.Ordinal))
            {
                _diffs.Add(new BaselineDiff
                {
                    Path = relativePath,
                    Expected = expected,
                    Actual = actual,
                });
            }
        }
    }

    public async Task UpdateCommittedAsync(CancellationToken cancellationToken = default)
    {
        await FinalizeOperationsAsync(cancellationToken).ConfigureAwait(false);
        var baselineRoot = Path.Combine(E2eEnvironment.BaselinesRoot, _baselineRelativePath);
        Directory.CreateDirectory(baselineRoot);
        foreach (var (relativePath, content) in _capturedFiles)
        {
            var fullPath = Path.Combine(baselineRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task FinalizeOperationsAsync(CancellationToken cancellationToken)
    {
        _capturedFiles["operations.json"] = _transcript.SerializeNormalized(_normalizer);
        return Task.CompletedTask;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
