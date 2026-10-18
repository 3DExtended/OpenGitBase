using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services.Lfs;

/// <summary>
/// The reconciler step "LFS orphans" (lfs-08): LFS data of repositories that no longer exist goes, also on a node
/// that was down while the repository was deleted.
/// </summary>
public sealed class LfsOrphanCleanerTests : IAsyncDisposable
{
    private const string NodeA = "storage-1";
    private const string NodeB = "storage-2";
    private const string Oid = "2222222222222222222222222222222222222222222222222222222222222222";

    private readonly ServiceProvider _provider;
    private readonly IQueryProcessor _queryProcessor = Substitute.For<IQueryProcessor>();
    private readonly FakeLfsStorageNodes _nodes = new();
    private readonly FakeStorageProvisionerClient _provisioner = new();
    private readonly RecordingLogger<LfsOrphanCleaner> _logger = new();
    private readonly HashSet<string> _healthy = new(StringComparer.Ordinal) { NodeA, NodeB };
    private readonly HashSet<string> _unreachableInventories = new(StringComparer.Ordinal);
    private readonly Guid _existing = Guid.NewGuid();
    private readonly Guid _deleted = Guid.NewGuid();

    public LfsOrphanCleanerTests()
    {
        _queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Option.From<IReadOnlyList<StorageNodeDto>>(_healthy.Select(Node).ToList()));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("node-token"));
        _provisioner.InventoryProvider = node => _unreachableInventories.Contains(node.InternalHost)
            ? StorageInventoryResult.Fail(502, "unreachable")
            : StorageInventoryResult.Ok(
                [],
                [],
                [.. new[] { _existing, _deleted }.Where(repositoryId => _nodes.Holds(node.InternalHost, repositoryId))]
            );

        var connection = SqliteTestConnection.OpenInMemory();
        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [
                    typeof(RepositoryMapsterConfig).Assembly,
                    typeof(global::OpenGitBase.Features.StorageNode.StorageNodeMapsterConfig).Assembly,
                ]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        _provider = services.BuildServiceProvider();
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    [Fact]
    public async Task RunOnceAsync_DeletesLfsDataOfARepositoryThatNoLongerExists()
    {
        await SeedExistingRepositoryAsync();
        StoreOn(NodeA, _existing, _deleted);
        StoreOn(NodeB, _deleted);

        await CleanAsync();

        Assert.False(_nodes.Holds(NodeA, _deleted));
        Assert.False(_nodes.Holds(NodeB, _deleted));
        Assert.True(_nodes.Holds(NodeA, _existing));
        Assert.Contains(
            _logger.Entries,
            entry => entry.Level == LogLevel.Information && entry.Message.Contains(_deleted.ToString(), StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task RunOnceAsync_CleansANodeThatWasDownOnceItIsBack()
    {
        await SeedExistingRepositoryAsync();
        StoreOn(NodeB, _deleted);
        _healthy.Remove(NodeB);

        await CleanAsync();
        Assert.True(_nodes.Holds(NodeB, _deleted));

        _healthy.Add(NodeB);
        await CleanAsync();
        Assert.False(_nodes.Holds(NodeB, _deleted));
    }

    [Fact]
    public async Task RunOnceAsync_ANodeThatCannotBeAskedDoesNotStopTheOthers()
    {
        await SeedExistingRepositoryAsync();
        StoreOn(NodeA, _deleted);
        StoreOn(NodeB, _deleted);
        _unreachableInventories.Add(NodeA);

        await CleanAsync();

        Assert.True(_nodes.Holds(NodeA, _deleted));
        Assert.False(_nodes.Holds(NodeB, _deleted));
    }

    [Fact]
    public async Task RunOnceAsync_AFailingDeleteDoesNotStopTheOthers()
    {
        await SeedExistingRepositoryAsync();
        StoreOn(NodeA, _deleted);
        StoreOn(NodeB, _deleted);
        _provisioner.InventoryProvider = _ => StorageInventoryResult.Ok([], [], [_deleted]);
        _nodes.FailingHosts.Add(NodeA);

        await CleanAsync();

        Assert.False(_nodes.Holds(NodeB, _deleted));
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RunOnceAsync_WithoutAnyRepository_DeletesNothing()
    {
        StoreOn(NodeA, _existing, _deleted);

        await CleanAsync();

        Assert.True(_nodes.Holds(NodeA, _existing));
        Assert.True(_nodes.Holds(NodeA, _deleted));
        Assert.DoesNotContain(_nodes.Requests, request => request.Method == HttpMethod.Delete);
    }

    private static StorageNodeDto Node(string host) =>
        new()
        {
            Id = StorageNodeId.From(FakeLfsStorageNodes.NodeId(host)),
            NodeId = host,
            InternalHost = host,
            InternalHttpPort = 8081,
            IsHealthy = true,
        };

    private void StoreOn(string host, params Guid[] repositoryIds)
    {
        foreach (var repositoryId in repositoryIds)
        {
            _nodes.Store(host, repositoryId, Oid, LfsObjectCopyKind.Plaintext, [1]);
        }
    }

    private async Task CleanAsync()
    {
        await using var context = await CreateContextAsync();
        var cleaner = new LfsOrphanCleaner(_queryProcessor, _provisioner, _nodes.CreateClient(), _logger);
        await cleaner.RunOnceAsync(context, CancellationToken.None);
    }

    private async Task SeedExistingRepositoryAsync()
    {
        await using var context = await CreateContextAsync();
        await context.Database.EnsureCreatedAsync();
        context.Set<RepositoryEntity>().Add(
            new RepositoryEntity
            {
                Id = _existing,
                Name = "kept",
                Slug = "kept",
                OwnerUserId = Guid.NewGuid(),
                PhysicalPath = $"/srv/git/{_existing}.git",
            }
        );
        await context.SaveChangesAsync();
    }

    private async Task<OpenGitBaseDbContext> CreateContextAsync()
    {
        var context = await _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>().CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
