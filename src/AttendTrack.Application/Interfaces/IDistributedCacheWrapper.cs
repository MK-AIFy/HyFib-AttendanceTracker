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

    /// <summary>
    /// Returns <c>true</c> when the key currently exists in Redis (or the in-memory
    /// fallback when Redis is unavailable). Cheaper and more reliable than
    /// <c>GetAsync&lt;object&gt;</c> when callers only need an existence check
    /// (e.g. kiosk heartbeat presence).
    /// </summary>
    Task<bool> KeyExistsAsync(string key, CancellationToken ct = default);
}
