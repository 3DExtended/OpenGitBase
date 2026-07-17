using System.Runtime.CompilerServices;
using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests;

public abstract class E2eTestBase : IAsyncLifetime
{
    protected E2eTestBase()
    {
        Context = new TestIsolation();
        Transcript = new OperationTranscript();
    }

    public virtual Task InitializeAsync() => Context.ClearCapturedEmailsAsync();

    public virtual Task DisposeAsync() => Task.CompletedTask;

    protected TestIsolation Context { get; }

    protected OperationTranscript Transcript { get; }

    private BaselineManager? _baselines;

    protected BaselineManager Baselines =>
        _baselines ?? throw new InvalidOperationException("Call BeginScenario() at the start of the test method.");

    protected static bool UpdateBaselines =>
        string.Equals(Environment.GetEnvironmentVariable("OPENGITBASE_E2E_UPDATE_BASELINES"), "1", StringComparison.Ordinal);

    protected void BeginScenario(string? scopeSuffix = null, [CallerMemberName] string scenarioName = "")
    {
        Transcript.Clear();
        var relativePath = string.IsNullOrWhiteSpace(scopeSuffix)
            ? $"{GetClassBaselinePath()}/{scenarioName}"
            : $"{GetClassBaselinePath()}/{scenarioName}/{scopeSuffix}";
        _baselines = new BaselineManager(relativePath, Context.Normalizer, Transcript, UpdateBaselines);
    }

    protected async Task AssertBaselinesAsync(
        CancellationToken cancellationToken = default,
        bool assertWhenCommitted = true)
    {
        if (UpdateBaselines)
        {
            await Baselines.UpdateCommittedAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!assertWhenCommitted)
        {
            return;
        }

        await Baselines.AssertMatchesCommittedAsync(cancellationToken).ConfigureAwait(false);
        if (Baselines.Diffs.Count > 0)
        {
            throw new InvalidOperationException(
                $"Baseline diffs: {string.Join(", ", Baselines.Diffs.Select(d => d.Path))}");
        }
    }

    private string GetClassBaselinePath() =>
        GetType().Namespace?.Replace("OpenGitBase.E2E.Tests.", string.Empty, StringComparison.Ordinal) + "/" + GetType().Name
        ?? GetType().Name;
}

public sealed class RequiresComposeFactAttribute : FactAttribute
{
    public RequiresComposeFactAttribute()
    {
        ComposeHealthGate.Refresh();
        if (!ComposeHealthGate.IsHealthy)
        {
            Skip = ComposeHealthGate.SkipReason;
        }
    }
}

public sealed class RequiresComposeTheoryAttribute : TheoryAttribute
{
    public RequiresComposeTheoryAttribute()
    {
        ComposeHealthGate.Refresh();
        if (!ComposeHealthGate.IsHealthy)
        {
            Skip = ComposeHealthGate.SkipReason;
        }
    }
}

public sealed class RequiresFullHaFactAttribute : FactAttribute
{
    public RequiresFullHaFactAttribute()
    {
        ComposeFullHaGate.Refresh();
        if (!ComposeFullHaGate.IsFullHaProfile)
        {
            Skip = ComposeFullHaGate.SkipReason;
        }

        ComposeHealthGate.Refresh();
        if (!ComposeHealthGate.IsHealthy)
        {
            Skip = ComposeHealthGate.SkipReason;
        }
    }
}

public sealed class RequiresFullHaTheoryAttribute : TheoryAttribute
{
    public RequiresFullHaTheoryAttribute()
    {
        ComposeFullHaGate.Refresh();
        if (!ComposeFullHaGate.IsFullHaProfile)
        {
            Skip = ComposeFullHaGate.SkipReason;
        }

        ComposeHealthGate.Refresh();
        if (!ComposeHealthGate.IsHealthy)
        {
            Skip = ComposeHealthGate.SkipReason;
        }
    }
}
