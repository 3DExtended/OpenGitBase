using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.ComputeNode;
using OpenGitBase.Features.ComputeNode.Entities;
using OpenGitBase.Features.ComputeNode.QueryHandlers;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository;

namespace OpenGitBase.Api.Tests.Integration;

internal sealed class OrgComputeTestScope : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private OrgComputeTestScope(SqliteConnection connection, ServiceProvider provider)
    {
        _connection = connection;
        _provider = provider;
    }

    public IDbContextFactory<OpenGitBaseDbContext> ContextFactory =>
        _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();

    public CreateComputeNodeEnrollmentQueryHandler CreateEnrollmentHandler =>
        _provider.GetRequiredService<CreateComputeNodeEnrollmentQueryHandler>();

    public RegisterComputeNodeQueryHandler RegisterHandler =>
        _provider.GetRequiredService<RegisterComputeNodeQueryHandler>();

    public ClaimPipelineJobQueryHandler ClaimHandler =>
        _provider.GetRequiredService<ClaimPipelineJobQueryHandler>();

    public UpdatePipelineJobStatusQueryHandler UpdateStatusHandler =>
        _provider.GetRequiredService<UpdatePipelineJobStatusQueryHandler>();

    public static async Task<OrgComputeTestScope> CreateAsync()
    {
        var connection = SqliteTestConnection.OpenInMemory();
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [
                    typeof(ComputeNodeMapsterConfig).Assembly,
                    typeof(PipelineMapsterConfig).Assembly,
                    typeof(RepositoryMapsterConfig).Assembly,
                ]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<IJobAvailableEventPublisher>(Substitute.For<IJobAvailableEventPublisher>());
        services.AddCqrs(options =>
            options
                .WithQueryHandlersFrom(typeof(CreateComputeNodeEnrollmentQueryHandler).Assembly)
                .WithQueryHandlersFrom(typeof(ClaimPipelineJobQueryHandler).Assembly)
        );
        var config = new TypeAdapterConfig();
        new ComputeNodeMapsterConfig().Register(config);
        new PipelineMapsterConfig().Register(config);
        new RepositoryMapsterConfig().Register(config);
        services.AddSingleton(config);
        services.AddSingleton<IMapper>(new Mapper(config));
        var provider = services.BuildServiceProvider();
        await using var context = await provider
            .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
            .CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        return new OrgComputeTestScope(connection, provider);
    }

    public async Task<Guid> GetNodeIdAsync(string nodeId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var node = await context
            .Set<ComputeNodeEntity>()
            .FirstAsync(entity => entity.NodeId == nodeId);
        return node.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
