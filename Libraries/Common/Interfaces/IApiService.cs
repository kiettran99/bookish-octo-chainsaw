namespace Common.Interfaces;

public interface IApiService
{
    Task<TResponse?> GetAsync<TResponse>(string baseUrl, string endpoint, IDictionary<string, string>? queryParameters = null, IDictionary<string, string>? headers = null);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string baseUrl, string endpoint, TRequest requestBody, IDictionary<string, string>? headers = null) where TRequest : class;
    Task<TResponse?> PutAsync<TRequest, TResponse>(string baseUrl, string endpoint, TRequest requestBody, IDictionary<string, string>? headers = null) where TRequest : class;
    Task<TResponse?> DeleteAsync<TResponse>(string baseUrl, string endpoint, IDictionary<string, string>? headers = null);
}
