namespace Common.Interfaces;

public interface IRedisService
{
    T? Get<T>(string key);
    Task<T?> GetAsync<T>(string key);
    Task<string?> GetStringAsync(string key);
    void Set<T>(string key, T value, int expiration);
    Task SetAsync<T>(string key, T value, int expiration);
    T GetByFunction<T>(string key, int expiration, Func<T> func);
    Task<T> GetByFunctionAsync<T>(string key, int expiration, Func<Task<T>> func);
    void Remove(string key);
    void RemoveByPattern(string pattern);
    void RemoveAllServicesByPattern(string pattern);
    Task RemoveAsync(string key);
    Task RemoveByPatternAsync(string pattern);
    Task RemoveByPatternAsync(string pattern, string instanceName);
}
