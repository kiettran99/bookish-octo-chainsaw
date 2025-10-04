using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Common.Implements;

public class RedisService : IRedisService
{
    private readonly IDistributedCache _cache;
    public readonly string _connectionString;
    private readonly string _host;
    private readonly string _port;
    private readonly string _instanceName;

    public RedisService(IDistributedCache cache, RedisOptions options)
    {
        _cache = cache;
        _connectionString = options.ConnectionString;
        _host = options.Host;
        _port = options.Port;
        _instanceName = options.InstanceName;
    }

    public T? Get<T>(string key)
    {
        var value = _cache.GetString(key);
        if (value != null)
        {
            return JsonSerializationHelper.Deserialize<T>(value);
        }

        return default;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _cache.GetStringAsync(key);
        if (value != null)
        {
            return JsonSerializationHelper.Deserialize<T>(value);
        }

        return default;
    }

    public async Task<string?> GetStringAsync(string key)
    {
        var value = await _cache.GetStringAsync(key);
        return value;
    }

    public void Set<T>(string key, T value, int expiration)
    {
        _cache.SetString(key, JsonSerializationHelper.Serialize(value), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expiration),
            SlidingExpiration = TimeSpan.FromMinutes(expiration)
        });
    }

    public async Task SetAsync<T>(string key, T value, int expiration)
    {
        await _cache.SetStringAsync(key, JsonSerializationHelper.Serialize(value), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expiration),
            SlidingExpiration = TimeSpan.FromMinutes(expiration)
        });
    }

    public T GetByFunction<T>(string key, int expiration, Func<T> func)
    {
        try
        {
            var result = Get<T>(key);

            if (result is null)
            {
                result = func();
                Set(key, result, expiration);
            }

            return result;
        }
        catch (Exception)
        {
            var result = func();
            return result;
        }
    }

    public async Task<T> GetByFunctionAsync<T>(string key, int expiration, Func<Task<T>> func)
    {
        try
        {
            var result = await GetAsync<T>(key);

            if (result is null)
            {
                result = await func();
                await SetAsync(key, result, expiration);
            }

            return result;
        }
        catch (Exception)
        {
            var result = await func();
            return result;
        }
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }

    public void RemoveByPattern(string pattern)
    {
        var redis = ConnectionMultiplexer.Connect(_connectionString);
        var server = redis.GetServer($"{_host}:{_port}");

        var keys = server.Keys(pattern: pattern);
        foreach (var key in keys)
        {
            Remove(key.ToString().Replace(_instanceName, string.Empty));
        }
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        using var redis = await ConnectionMultiplexer.ConnectAsync(_connectionString);
        var server = redis.GetServer($"{_host}:{_port}");

        var keys = server.KeysAsync(pattern: pattern);
        await foreach (var key in keys)
        {
            await RemoveAsync(key.ToString().Replace(_instanceName, string.Empty));
        }
    }

    public void RemoveAllServicesByPattern(string pattern)
    {
        using ConnectionMultiplexer cm = ConnectionMultiplexer.Connect(_connectionString);
        var database = cm.GetDatabase(0);
        var server = cm.GetServer($"{_host}:{_port}");

        foreach (var key in server.Keys(0, pattern))
        {
            database.KeyDelete(key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, string instanceName)
    {
        using var redis = await ConnectionMultiplexer.ConnectAsync(_connectionString);
        var server = redis.GetServer($"{_host}:{_port}");

        var keys = server.KeysAsync(pattern: pattern);
        await foreach (var key in keys)
        {
            await RemoveAsync(key.ToString().Replace(instanceName, string.Empty));
        }
    }
}