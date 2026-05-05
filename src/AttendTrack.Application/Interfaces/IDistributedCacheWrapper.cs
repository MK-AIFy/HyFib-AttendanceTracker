namespace AttendTrack.Application.Interfaces;

/// <summary>
/// [Gap 7] Redis-backed distributed cache with automatic in-memory fallback.
/// Inject this instead of IDistributedCache to get resilient caching that
/// degrades gracefully when Redis is unreachable.
/// </summary>
public interface IDistributedCacheWrapper
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
