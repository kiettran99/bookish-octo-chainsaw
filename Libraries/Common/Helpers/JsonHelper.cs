using System.Text.Json;

namespace Common.Helpers;

public static class JsonHelper
{
    public static T? ParseJson<T>(string? jsonString) where T : class
    {
        if (string.IsNullOrEmpty(jsonString))
        {
            return null;
        }

        try
        {
            return JsonSerializationHelper.Deserialize<T>(jsonString);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Optional: Add a method for value types that returns nullable value types
    public static T? ParseJsonValue<T>(string? jsonString) where T : struct
    {
        if (string.IsNullOrEmpty(jsonString))
        {
            return null;
        }

        try
        {
            return JsonSerializationHelper.Deserialize<T>(jsonString);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
