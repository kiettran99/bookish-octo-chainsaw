using Common.Helpers;
using Common.Interfaces;
using StackExchange.Redis;

namespace Common.Implements;

public class RedisBackgroundService : IRedisBackgroundService
{
    private readonly IDatabase _database;

    public RedisBackgroundService(IConnectionMultiplexer mux)
    {
        _database = mux.GetDatabase();
    }


    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _database.StringGetAsync(key);
        if (value.IsNullOrEmpty)
        {
            return default;
        }
        return JsonSerializationHelper.Deserialize<T?>(value.ToString());
    }

    public async Task SetAsync<T>(string key, T value, int expirationMinutes)
    {
        var redisValue = new RedisValue(JsonSerializationHelper.Serialize(value));
        var expiration = TimeSpan.FromMinutes(expirationMinutes);

        await _database.StringSetAsync(key, redisValue, expiration);
    }

    public async Task RemoveAsync(string key)
    {
        await _database.KeyDeleteAsync(key);
    }
}
