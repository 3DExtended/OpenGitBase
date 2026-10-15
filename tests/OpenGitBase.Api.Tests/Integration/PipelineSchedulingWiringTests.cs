using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Api.Tests.Integration;

/// <summary>
/// The push outbox worker resolves the scheduler in a background scope, so a missing
/// registration only shows up in production, as pushes that never start a pipeline.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class PipelineSchedulingWiringTests
{
    [Fact]
    public async Task TheApplicationContainer_ReadsCiFilesFromStorageWhenSchedulingFromAPush()
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

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GitPushOutboxDrainService>());
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<
                IQueryHandler<SchedulePipelineRunFromPushQuery, PipelineRunId>
            >()
        );
        Assert.IsType<StorageRepositoryFileReader>(
            scope.ServiceProvider.GetRequiredService<IRepositoryFileReader>()
        );
    }
}
