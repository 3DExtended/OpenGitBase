using Microsoft.Extensions.DependencyInjection;

namespace OpenGitBase.Cqrs.DependencyInjection;

internal sealed class ServiceProviderQueryHandlerFactory : IQueryHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceProviderQueryHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public THandler CreateQueryHandler<THandler, TQuery, TResult>()
        where THandler : class, IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult, TQuery> => _serviceProvider.GetRequiredService<THandler>();
}
