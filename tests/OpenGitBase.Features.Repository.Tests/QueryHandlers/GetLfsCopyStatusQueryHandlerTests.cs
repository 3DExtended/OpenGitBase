using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class GetLfsCopyStatusQueryHandlerTests
{
    private static readonly Guid Primary = Guid.NewGuid();
    private static readonly Guid EncryptedA = Guid.NewGuid();
    private static readonly Guid EncryptedB = Guid.NewGuid();

    [Fact]
    public async Task RunQueryAsync_CountsTheCopiesEachNodeHoldsAndWhatItStillMisses()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        var repositoryId = await SeedAsync(
            scope,
            objects: [("a", 100), ("b", 250), ("c", 50)],
            copies:
            [
                ("a", Primary, LfsObjectCopyKind.Plaintext),
                ("a", EncryptedA, LfsObjectCopyKind.Encrypted),
                ("b", EncryptedA, LfsObjectCopyKind.Encrypted),
                ("c", EncryptedA, LfsObjectCopyKind.Encrypted),
                ("a", EncryptedB, LfsObjectCopyKind.Encrypted),
            ]
        );

        var result = await Handler(scope).RunQueryAsync(new GetLfsCopyStatusQuery { RepositoryId = repositoryId }, CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(
            result,
            status =>
            {
                Assert.Equal((3, 400L), (status.ObjectCount, status.TotalBytes));
                var nodes = status.Nodes.ToDictionary(node => node.StorageNodeId);
                Assert.Equal((LfsObjectCopyKind.Plaintext, true, 1, 2), Describe(nodes[Primary]));
                Assert.Equal((LfsObjectCopyKind.Encrypted, false, 3, 0), Describe(nodes[EncryptedA]));
                Assert.Equal((LfsObjectCopyKind.Encrypted, false, 1, 2), Describe(nodes[EncryptedB]));
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_ListsThePrimaryEvenWhenItHasNoReplicaRow()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        var repositoryId = await SeedAsync(scope, objects: [("a", 1)], copies: [], primaryHasReplicaRow: false);

        var result = await Handler(scope).RunQueryAsync(new GetLfsCopyStatusQuery { RepositoryId = repositoryId }, CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(
            result,
            status => Assert.Equal((LfsObjectCopyKind.Plaintext, true, 0, 1), Describe(status.Nodes.Single(node => node.StorageNodeId == Primary)))
        );
    }

    [Fact]
    public async Task RunQueryAsync_ForARepositoryWithoutLfsObjects_ReportsNothingMissing()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        var repositoryId = await SeedAsync(scope, objects: [], copies: []);

        var result = await Handler(scope).RunQueryAsync(new GetLfsCopyStatusQuery { RepositoryId = repositoryId }, CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(
            result,
            status =>
            {
                Assert.Equal((0, 0L), (status.ObjectCount, status.TotalBytes));
                Assert.All(status.Nodes, node => Assert.Equal(0, node.Missing));
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_ForAnUnknownRepository_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        var result = await Handler(scope).RunQueryAsync(
            new GetLfsCopyStatusQuery { RepositoryId = RepositoryId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }

    private static (LfsObjectCopyKind Kind, bool IsPrimary, int Copies, int Missing) Describe(LfsNodeCopyStatusDto node) =>
        (node.Kind, node.IsPrimary, node.Copies, node.Missing);

    private static GetLfsCopyStatusQueryHandler Handler(InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> scope) =>
        new(scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>());

    private static async Task<RepositoryId> SeedAsync(
        InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> scope,
        (string Oid, long Size)[] objects,
        (string Oid, Guid Node, LfsObjectCopyKind Kind)[] copies,
        bool primaryHasReplicaRow = true
    )
    {
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        var repository = await context.Set<RepositoryEntity>().SingleAsync(entity => entity.Id == repositoryId.Value);
        repository.PrimaryStorageNodeId = Primary;
        foreach (var (node, role) in new[]
        {
            (Primary, RepositoryReplicaRole.Primary),
            (EncryptedA, RepositoryReplicaRole.EncryptedReplica),
            (EncryptedB, RepositoryReplicaRole.EncryptedReplica),
        })
        {
            if (node != Primary || primaryHasReplicaRow)
            {
                context.Set<RepositoryReplicaEntity>().Add(
                    new RepositoryReplicaEntity { RepositoryId = repositoryId.Value, StorageNodeId = node, Role = role }
                );
            }
        }

        foreach (var (oid, size) in objects)
        {
            context.Set<LfsObjectEntity>().Add(
                new LfsObjectEntity { RepositoryId = repositoryId.Value, Oid = Oid(oid), Size = size, KeyVersion = 1 }
            );
        }

        foreach (var (oid, node, kind) in copies)
        {
            context.Set<LfsObjectCopyEntity>().Add(
                new LfsObjectCopyEntity
                {
                    RepositoryId = repositoryId.Value,
                    Oid = Oid(oid),
                    StorageNodeId = node,
                    Kind = kind,
                    StoredAt = DateTimeOffset.UtcNow,
                }
            );
        }

        await context.SaveChangesAsync();
        return repositoryId;
    }

    private static string Oid(string letter) => string.Concat(Enumerable.Repeat(letter, 64));

    private static InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> CreateScope() =>
        new(typeof(GetLfsCopyStatusQueryHandler).Assembly, typeof(UserEntity).Assembly);
}
