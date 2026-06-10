using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace OpenGitBase.Cqrs.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCqrs(
        this IServiceCollection serviceCollection,
        Action<CqrsOptions> configure
    )
    {
        var options = new CqrsOptions();
        configure(options);

        serviceCollection.AddScoped<IQueryHandlerFactory, ServiceProviderQueryHandlerFactory>();
        serviceCollection.AddScoped<IQueryProcessor, QueryProcessor>();

        var queryHandlerTypes = options
            .AssembliesToLoadQueryHandlersFrom.SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && IsQueryHandlerType(t))
            .ToList();

        foreach (var handlerType in queryHandlerTypes)
        {
            serviceCollection.AddTransient(handlerType);

            if (handlerType.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (var queryHandlerInterface in GetQueryHandlerInterfaces(handlerType))
            {
                serviceCollection.AddTransient(queryHandlerInterface, handlerType);
            }
        }

        return serviceCollection;
    }

    private static IReadOnlyList<Type> GetQueryHandlerInterfaces(Type type) =>
        type.GetInterfaces()
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))
            .ToList();

    private static bool IsQueryHandlerType(Type type) =>
        type.GetInterfaces()
            .Where(t => t.IsGenericType)
            .Select(t => t.GetGenericTypeDefinition())
            .Any(t => t == typeof(IQueryHandler<,>));
}
