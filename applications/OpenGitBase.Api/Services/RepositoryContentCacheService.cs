using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace OpenGitBase.Api.Services;

public sealed class RepositoryContentCacheService : IRepositoryContentCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDistributedCache _cache;
    private readonly ILogger<RepositoryContentCacheService> _logger;

    public RepositoryContentCacheService(
        IDistributedCache cache,
        ILogger<RepositoryContentCacheService> logger
    )
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var bytes = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(bytes, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Repository content cache read failed for key {CacheKey}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken
    )
        where T : class
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            await _cache
                .SetAsync(
                    key,
                    bytes,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Repository content cache write failed for key {CacheKey}", key);
        }
    }
}
