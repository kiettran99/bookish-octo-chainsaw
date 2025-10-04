using System.Text.Json;
using Common.Interfaces;
using RestSharp;

namespace Common.Implements;

public class ApiService : IApiService
{
    public async Task<TResponse?> GetAsync<TResponse>(string baseUrl, string endpoint, IDictionary<string, string>? queryParameters = null, IDictionary<string, string>? headers = null)
    {
        var client = new RestClient(baseUrl);
        var request = new RestRequest(endpoint, Method.Get);
        AddHeaders(request, headers);
        AddQueryParameters(request, queryParameters);

        var response = await client.ExecuteAsync<TResponse>(request);
        if (!response.IsSuccessful)
        {
            return default;
        }
        return response.Data;
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string baseUrl, string endpoint, TRequest requestBody, IDictionary<string, string>? headers = null)
        where TRequest : class
    {
        var client = new RestClient(baseUrl);
        var request = new RestRequest(endpoint, Method.Post);
        AddHeaders(request, headers);
        request.AddBody(JsonSerializer.Serialize(requestBody));

        var response = await client.ExecuteAsync<TResponse>(request);
        if (!response.IsSuccessful)
        {
            return default;
        }
        return response.Data;
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(string baseUrl, string endpoint, TRequest requestBody, IDictionary<string, string>? headers = null)
        where TRequest : class
    {
        var client = new RestClient(baseUrl);
        var request = new RestRequest(endpoint, Method.Put);
        AddHeaders(request, headers);
        request.AddBody(JsonSerializer.Serialize(requestBody));

        var response = await client.ExecuteAsync<TResponse>(request);
        if (!response.IsSuccessful)
        {
            return default;
        }
        return response.Data;
    }

    public async Task<TResponse?> DeleteAsync<TResponse>(string baseUrl, string endpoint, IDictionary<string, string>? headers = null)
    {
        var client = new RestClient(baseUrl);
        var request = new RestRequest(endpoint, Method.Delete);
        AddHeaders(request, headers);

        var response = await client.ExecuteAsync<TResponse>(request);
        if (!response.IsSuccessful)
        {
            return default;
        }
        return response.Data;
    }

    private static void AddHeaders(RestRequest request, IDictionary<string, string>? headers)
    {
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.AddHeader(header.Key, header.Value);
            }
        }
    }

    private static void AddQueryParameters(RestRequest request, IDictionary<string, string>? queryParameters)
    {
        if (queryParameters != null)
        {
            foreach (var queryParam in queryParameters)
            {
                request.AddQueryParameter(queryParam.Key, queryParam.Value);
            }
        }
    }
}
