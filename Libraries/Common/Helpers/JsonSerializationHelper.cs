using System.Text.Json;

namespace Common.Helpers;

public static class JsonSerializationHelper
{
    private static readonly JsonSerializerOptions s_writeOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions s_readOptions = new()
    {
        AllowTrailingCommas = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, s_writeOptions);
    }

    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, s_readOptions);
    }
}
