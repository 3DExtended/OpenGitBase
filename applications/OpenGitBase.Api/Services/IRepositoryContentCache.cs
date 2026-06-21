namespace OpenGitBase.Api.Services;

public interface IRepositoryContentCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
        where T : class;

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken
    )
        where T : class;
}
