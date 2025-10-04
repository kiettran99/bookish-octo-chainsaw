namespace Common.Models.ExceptionModels;

public class ApiExceptionResponse : ApiResponse
{
    public ApiExceptionResponse(int statusCode, string? message = null, string? description = null) : base(statusCode, message)
    {
        Description = description;
    }

    public string? Description { get; }
}
