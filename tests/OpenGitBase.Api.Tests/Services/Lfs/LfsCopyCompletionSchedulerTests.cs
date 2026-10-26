using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Services.Lfs;

public class LfsCopyCompletionSchedulerTests
{
    [Fact]
    public async Task Schedule_CompletesTheCopiesInAScopeOfItsOwn()
    {
        var content = RandomNumberGenerator.GetBytes(100);
        var oid = Convert.ToHexStringLower(SHA256.HashData(content));
        var repositoryId = Guid.NewGuid();
        var nodes = new FakeLfsStorageNodes();
        nodes.Store("storage-1", repositoryId, oid, LfsObjectCopyKind.Plaintext, content);
        var recorded = new TaskCompletionSource<RecordLfsObjectCopyQuery>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<RecordLfsObjectCopyQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                recorded.TrySetResult(callInfo.Arg<RecordLfsObjectCopyQuery>());
                return Option.From(new RecordLfsObjectResult { Created = true });
            });
        var services = new ServiceCollection();
        services.AddScoped(_ => new LfsReplicationService(
            queryProcessor,
            nodes.CreateClient(),
            Substitute.For<IRepositoryKeyService>(),
            new LfsOptions(),
            NullLogger<LfsReplicationService>.Instance
        ));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var replicas = new LfsReplicaSet(
            repositoryId,
            FakeLfsStorageNodes.Endpoint("storage-1"),
            [FakeLfsStorageNodes.Endpoint("storage-4")],
            []
        );

        new LfsCopyCompletionScheduler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LfsCopyCompletionScheduler>.Instance
        ).Schedule(
            new LfsPendingCopies(
                replicas,
                oid,
                content.Length,
                new LfsCipherContext(new byte[32], 1, repositoryId, oid),
                [],
                new HashSet<Guid>()
            )
        );

        var copy = await recorded.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal((FakeLfsStorageNodes.NodeId("storage-4"), LfsObjectCopyKind.Plaintext), (copy.StorageNodeId, copy.Kind));
        Assert.Equal(content, nodes.Find("storage-4", repositoryId, oid, LfsObjectCopyKind.Plaintext));
    }
}
