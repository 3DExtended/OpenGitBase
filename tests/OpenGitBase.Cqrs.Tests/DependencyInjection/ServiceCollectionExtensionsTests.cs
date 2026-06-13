using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCqrs_RegistersProcessorFactoryAndHandlers()
    {
        var services = new ServiceCollection();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(StubStringQueryHandler).Assembly)
        );

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IQueryHandlerFactory>());
        Assert.NotNull(provider.GetService<IQueryProcessor>());
        Assert.NotNull(provider.GetService<StubStringQueryHandler>());
        Assert.NotNull(
            provider.GetService<IQueryHandler<StubStringQuery, string>>()
        );
    }

    [Fact]
    public void AddCqrs_SkipsAbstractHandlers()
    {
        var services = new ServiceCollection();
        services.AddCqrs(options => options.WithQueryHandlersFrom(typeof(AbstractStubHandler).Assembly));

        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<AbstractStubHandler>());
    }

    [Fact]
    public void AddCqrs_OpenGenericHandler_IsRegisteredWithoutInterfaceMapping()
    {
        var services = new ServiceCollection();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(OpenGenericStubHandler<>).Assembly)
        );

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IQueryHandler<StubCountQuery, int>>()
        );
    }
}
