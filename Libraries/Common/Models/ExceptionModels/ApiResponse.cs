namespace Common.Models.ExceptionModels;

public class ApiResponse
{
    public int StatusCode { get; }
    public string? Message { get; }

    public ApiResponse(int statusCode, string? message = null)
    {
        StatusCode = statusCode;
        Message = message ?? GetDefaultMessageForStatusCode(statusCode);
    }

    public static string? GetDefaultMessageForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            400 => "Bad Request, make sure params compitable.",
            401 => "Authorized ! Make sure you has token to access",
            404 => "Not Found ! Make sure you routing correct.",
            500 => "Server errors ! Sorry please try next request.",
            _ => null
        };
    }
}
