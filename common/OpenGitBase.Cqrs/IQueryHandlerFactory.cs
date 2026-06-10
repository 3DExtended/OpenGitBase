namespace OpenGitBase.Cqrs;

public interface IQueryHandlerFactory
{
    THandler CreateQueryHandler<THandler, TQuery, TResult>()
        where THandler : class, IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult, TQuery>;
}
