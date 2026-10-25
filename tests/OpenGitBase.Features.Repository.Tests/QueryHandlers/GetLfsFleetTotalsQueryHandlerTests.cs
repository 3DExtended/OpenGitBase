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

public class GetLfsFleetTotalsQueryHandlerTests
{
    private static readonly Guid Primary = Guid.NewGuid();
    private static readonly Guid Encrypted = Guid.NewGuid();

    [Fact]
    public async Task RunQueryAsync_SumsObjectsBytesAndMissingCopiesOverEveryRepository()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using (var context = await scope.CreateDbContextAsync())
        {
            // Complete: both nodes hold both objects.
            await SeedAsync(
                context,
                "complete",
                objects: [("a", 100), ("b", 200)],
                copies:
                [
                    ("a", Primary, LfsObjectCopyKind.Plaintext),
                    ("b", Primary, LfsObjectCopyKind.Plaintext),
                    ("a", Encrypted, LfsObjectCopyKind.Encrypted),
                    ("b", Encrypted, LfsObjectCopyKind.Encrypted),
                ]
            );

            // Restoring: the primary misses two objects, the encrypted replica one; a plaintext copy on the
            // encrypted replica's node does not count for it.
            await SeedAsync(
                context,
                "restoring",
                objects: [("c", 10), ("d", 20), ("e", 30)],
                copies:
                [
                    ("c", Primary, LfsObjectCopyKind.Plaintext),
                    ("c", Encrypted, LfsObjectCopyKind.Encrypted),
                    ("d", Encrypted, LfsObjectCopyKind.Encrypted),
                    ("e", Encrypted, LfsObjectCopyKind.Plaintext),
                ]
            );

            await SeedAsync(context, "no-lfs", objects: [], copies: []);
        }

        var result = await Handler(scope).RunQueryAsync(new GetLfsFleetTotalsQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(
            result,
            totals => Assert.Equal(
                (5, 360L, 3, 1),
                (totals.ObjectCount, totals.TotalBytes, totals.MissingCopies, totals.RepositoriesWithMissingCopies)
            )
        );
    }

    [Fact]
    public async Task RunQueryAsync_WithoutLfsObjects_IsZero()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using (var context = await scope.CreateDbContextAsync())
        {
            await SeedAsync(context, "no-lfs", objects: [], copies: []);
        }

        var result = await Handler(scope).RunQueryAsync(new GetLfsFleetTotalsQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(
            result,
            totals => Assert.Equal(
                (0, 0L, 0, 0),
                (totals.ObjectCount, totals.TotalBytes, totals.MissingCopies, totals.RepositoriesWithMissingCopies)
            )
        );
    }

    private static GetLfsFleetTotalsQueryHandler Handler(InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> scope) =>
        new(scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>());

    private static async Task SeedAsync(
        OpenGitBaseDbContext context,
        string slug,
        (string Oid, long Size)[] objects,
        (string Oid, Guid Node, LfsObjectCopyKind Kind)[] copies
    )
    {
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(context, ownerUsername: $"owner-{slug}", slug: slug);
        var repository = await context.Set<RepositoryEntity>().SingleAsync(entity => entity.Id == repositoryId.Value);
        repository.PrimaryStorageNodeId = Primary;
        context.Set<RepositoryReplicaEntity>().AddRange(
            new RepositoryReplicaEntity { RepositoryId = repositoryId.Value, StorageNodeId = Primary, Role = RepositoryReplicaRole.Primary },
            new RepositoryReplicaEntity { RepositoryId = repositoryId.Value, StorageNodeId = Encrypted, Role = RepositoryReplicaRole.EncryptedReplica }
        );
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
    }

    private static string Oid(string letter) => string.Concat(Enumerable.Repeat(letter, 64));

    private static InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> CreateScope() =>
        new(typeof(GetLfsFleetTotalsQueryHandler).Assembly, typeof(UserEntity).Assembly);
}
