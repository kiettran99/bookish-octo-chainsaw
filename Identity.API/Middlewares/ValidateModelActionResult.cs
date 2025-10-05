using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Middlewares;

public class ValidateModelActionResult : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var state = context.ModelState;

        if (!state.IsValid)
        {
            var key = state.Keys.FirstOrDefault(key => state[key]?.Errors.Count > 0);
            if (!string.IsNullOrEmpty(key))
            {
                context.HttpContext.Response.StatusCode = 400;
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    IsSuccess = false,
                    ErrorMessage = state[key]?.Errors[0].ErrorMessage ?? string.Empty
                });
            }
        }
    }
}