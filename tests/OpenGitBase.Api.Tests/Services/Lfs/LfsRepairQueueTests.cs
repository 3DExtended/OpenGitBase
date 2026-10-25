using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services.Lfs;

public sealed class LfsRepairQueueTests : IAsyncDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private readonly FakeReconciler _reconciler = new();
    private readonly ServiceProvider _provider;
    private readonly LfsRepairQueue _queue;

    public LfsRepairQueueTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_reconciler);
        services.AddScoped<ScopeMarker>();
        services.AddScoped<ILfsCopyReconciler>(provider =>
        {
            _reconciler.Scopes.Add(provider.GetRequiredService<ScopeMarker>());
            return _reconciler;
        });
        _provider = services.BuildServiceProvider();
        _queue = new LfsRepairQueue(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<LfsRepairQueue>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        _reconciler.Release.TrySetResult();
        await _queue.StopAsync(CancellationToken.None);
        _queue.Dispose();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Schedule_ReconcilesTheRepositoryInADiScopeOfItsOwn()
    {
        _reconciler.Release.SetResult();
        await _queue.StartAsync(CancellationToken.None);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        _queue.Schedule(first);
        _queue.Schedule(second);

        await WaitUntilAsync(() => _reconciler.Runs.Count == 2);
        Assert.Equal([first, second], _reconciler.Runs);
        Assert.Equal(2, _reconciler.Scopes.Distinct().Count());
    }

    [Fact]
    public async Task Schedule_OfARepositoryAlreadyWaiting_CollapsesIntoOneRun()
    {
        await _queue.StartAsync(CancellationToken.None);
        var busy = Guid.NewGuid();
        var repeated = Guid.NewGuid();
        _queue.Schedule(busy);
        await WaitUntilAsync(() => _reconciler.Runs.Count == 1);

        for (var i = 0; i < 20; i++)
        {
            _queue.Schedule(repeated);
        }

        _reconciler.Release.SetResult();
        await WaitUntilAsync(() => _reconciler.Runs.Count == 2);
        await Task.Delay(200);
        Assert.Equal([busy, repeated], _reconciler.Runs);
    }

    [Fact]
    public async Task ARunThatLeavesRepairsForLater_QueuesTheRepositoryAgain()
    {
        _reconciler.Release.SetResult();
        var repository = Guid.NewGuid();
        _reconciler.Results.Enqueue(new LfsReconcileResult(Missing: 150, Repaired: 100, Failed: 0, Remaining: 50));
        _reconciler.Results.Enqueue(new LfsReconcileResult(Missing: 50, Repaired: 50, Failed: 0, Remaining: 0));
        await _queue.StartAsync(CancellationToken.None);

        _queue.Schedule(repository);

        await WaitUntilAsync(() => _reconciler.Runs.Count == 2);
        await Task.Delay(200);
        Assert.Equal([repository, repository], _reconciler.Runs);
    }

    [Fact]
    public async Task ARunThatRepairedNothing_IsNotQueuedAgainRightAway()
    {
        _reconciler.Release.SetResult();
        _reconciler.Results.Enqueue(new LfsReconcileResult(Missing: 150, Repaired: 0, Failed: 100, Remaining: 50));
        await _queue.StartAsync(CancellationToken.None);

        _queue.Schedule(Guid.NewGuid());

        await WaitUntilAsync(() => _reconciler.Runs.Count == 1);
        await Task.Delay(300);
        Assert.Single(_reconciler.Runs);
    }

    [Fact]
    public async Task AFailingRun_DoesNotStopTheWorker()
    {
        _reconciler.Release.SetResult();
        var failing = Guid.NewGuid();
        var next = Guid.NewGuid();
        _reconciler.Failing.Add(failing);
        await _queue.StartAsync(CancellationToken.None);

        _queue.Schedule(failing);
        _queue.Schedule(next);

        await WaitUntilAsync(() => _reconciler.Runs.Count == 2);
        Assert.Equal([failing, next], _reconciler.Runs);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + Deadline;
        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The repair queue did not get there in time.");
            await Task.Delay(20);
        }
    }

    private sealed class ScopeMarker;

    private sealed class FakeReconciler : ILfsCopyReconciler
    {
        public ConcurrentQueue<LfsReconcileResult> Results { get; } = new();

        public ConcurrentBag<ScopeMarker> Scopes { get; } = [];

        public HashSet<Guid> Failing { get; } = [];

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<Guid> Runs { get; } = [];

        public async Task<LfsReconcileResult> ReconcileRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken)
        {
            lock (Runs)
            {
                Runs.Add(repositoryId);
            }

            await Release.Task.WaitAsync(cancellationToken);
            if (Failing.Contains(repositoryId))
            {
                throw new HttpRequestException("storage-2 is down");
            }

            return Results.TryDequeue(out var result) ? result : LfsReconcileResult.Nothing;
        }
    }
}
