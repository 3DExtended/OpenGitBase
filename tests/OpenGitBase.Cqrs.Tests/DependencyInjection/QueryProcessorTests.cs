using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.DependencyInjection;

public class QueryProcessorTests
{
    [Fact]
    public async Task RunQueryAsync_UsesFactoryToResolveHandler()
    {
        var services = new ServiceCollection();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(StubStringQueryHandler).Assembly)
        );

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IQueryProcessor>();

        var result = await processor.RunQueryAsync(new StubStringQuery(), CancellationToken.None);

        Assert.True(result.IsSome);
        Assert.Equal(StubStringQueryHandler.Result, result.Get());
    }

    [Fact]
    public async Task RunQueryAsync_WithSubstitutedFactory_DelegatesToHandler()
    {
        var handler = Substitute.For<IQueryHandler<StubStringQuery, string>>();
        handler
            .RunQueryAsync(Arg.Any<StubStringQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("from-substitute"));

        var factory = Substitute.For<IQueryHandlerFactory>();
        factory
            .CreateQueryHandler<
                IQueryHandler<StubStringQuery, string>,
                StubStringQuery,
                string
            >()
            .Returns(handler);

        var processor = new QueryProcessor(factory);
        var result = await processor.RunQueryAsync(new StubStringQuery(), CancellationToken.None);

        Assert.Equal("from-substitute", result.Get());
        await handler.Received(1).RunQueryAsync(Arg.Any<StubStringQuery>(), Arg.Any<CancellationToken>());
    }
}
