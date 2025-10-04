namespace Common.Interfaces;

public interface IRedisBackgroundService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, int expirationMinutes);
    Task RemoveAsync(string key);
}
