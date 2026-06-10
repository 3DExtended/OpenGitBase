namespace OpenGitBase.Cqrs;

public sealed class QueryProcessor : IQueryProcessor
{
    private readonly IQueryHandlerFactory _queryHandlerFactory;

    public QueryProcessor(IQueryHandlerFactory queryHandlerFactory)
    {
        _queryHandlerFactory = queryHandlerFactory;
    }

    public Task<Option<TResult>> RunQueryAsync<TQuery, TResult>(
        IQuery<TResult, TQuery> query,
        CancellationToken cancellationToken
    )
        where TQuery : IQuery<TResult, TQuery>
    {
        var handler = _queryHandlerFactory.CreateQueryHandler<
            IQueryHandler<TQuery, TResult>,
            TQuery,
            TResult
        >();
        return handler.RunQueryAsync((TQuery)query, cancellationToken);
    }
}
