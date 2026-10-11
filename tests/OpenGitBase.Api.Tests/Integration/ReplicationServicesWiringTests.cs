using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Integration;

/// <summary>
/// The replication services are resolved per request or per reconciler pass, so a missing
/// registration only shows up in production, as a failed push replication or a dead reconciler.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ReplicationServicesWiringTests
{
    [Fact]
    public async Task TheApplicationContainer_ResolvesTheQuorumHandlerTheReconcilerAndTheCatchUp()
    {
        using var connection = SqliteTestConnection.OpenInMemory();
        await using var factory = new WebApplicationFactory<ApiEntryPoint>().WithWebHostBuilder(
            builder =>
            {
                builder.UseEnvironment("E2ETest");
                builder.ConfigureServices(services =>
                {
                    AuthTestServerConfiguration.ConfigureDatabase(services, connection);
                    AuthTestServerConfiguration.ConfigureOptions(services);
                    AuthTestServerConfiguration.ConfigureGoogleValidator(services);
                });
            }
        );

        using var scope = factory.Services.CreateScope();

        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<
                IQueryHandler<QuorumReplicateRepositoryQuery, QuorumReplicateRepositoryResult>
            >()
        );
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AntiEntropyReconcilerService>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<IQueryHandler<MarkPrimaryReplicatingQuery, bool>>()
        );
        // Resolved in a scope of its own after a push has been answered.
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ReplicaCatchUpService>());
    }
}
