using System.Text.Json;
using AttendTrack.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// [Gap 7] Redis-backed distributed cache with automatic IMemoryCache fallback.
/// Catches RedisConnectionException and SocketException transparently so a Redis
/// outage never brings down the application.
/// </summary>
public sealed class DistributedCacheWrapper : IDistributedCacheWrapper
{
    private readonly IDistributedCache _redis;
    private readonly IMemoryCache      _memoryCache;
    private readonly ILogger<DistributedCacheWrapper> _logger;

    private static readonly JsonSerializerOptions _jsonOpts =
        new(JsonSerializerDefaults.Web);

    public DistributedCacheWrapper(
        IDistributedCache                  redis,
        IMemoryCache                       memoryCache,
        ILogger<DistributedCacheWrapper>   logger)
    {
        _redis       = redis;
        _memoryCache = memoryCache;
        _logger      = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await _redis.GetAsync(key, ct).ConfigureAwait(false);
            if (bytes is null) return default;
            return JsonSerializer.Deserialize<T>(bytes, _jsonOpts);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis GET failed for key '{Key}', using IMemoryCache", key);
            return _memoryCache.TryGetValue(key, out T? val) ? val : default;
        }
    }

    public async Task SetAsync<T>(
        string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _jsonOpts);

        var opts = new DistributedCacheEntryOptions();
        if (expiry.HasValue) opts.SetAbsoluteExpiration(expiry.Value);

        try
        {
            await _redis.SetAsync(key, bytes, opts, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis SET failed for key '{Key}', writing to IMemoryCache", key);
        }

        // Always write to memory cache as well (covers Redis failure)
        _memoryCache.Set(key, value, expiry ?? TimeSpan.FromMinutes(5));
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _redis.RemoveAsync(key, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis REMOVE failed for key '{Key}'", key);
        }

        _memoryCache.Remove(key);
    }

    public async Task<bool> KeyExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await _redis.GetAsync(key, ct).ConfigureAwait(false);
            if (bytes is not null) return true;
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "Redis EXISTS failed for key '{Key}', falling back", key);
        }

        return _memoryCache.TryGetValue(key, out _);
    }

    private static bool IsRedisFailure(Exception ex) =>
        ex is RedisConnectionException
            or RedisTimeoutException
            or System.Net.Sockets.SocketException;
}

