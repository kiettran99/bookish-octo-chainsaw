using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace CineReview.API.Controllers;

public class CommonController : ControllerBase
{
    [NonAction]
    public int? GetUserIdByToken()
    {
        var userId = User.FindFirstValue("id");
        if (int.TryParse(userId, out int id))
        {
            return id;
        }
        return null;
    }

    [NonAction]
    public List<string> GetUserRolesByToken()
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
        return roles;
    }

    [NonAction]
    public string? IpAddress()
    {
        var ipAddress = GetIpAddress();
        return ipAddress?.Split(',').FirstOrDefault();
    }

    private string? GetIpAddress()
    {
        // get source ip address for the current request
        if (Request.Headers.TryGetValue("CF-Connecting-IP", out Microsoft.Extensions.Primitives.StringValues cfValue))
            return cfValue;
        else if (Request.Headers.TryGetValue("X-Forwarded-For", out Microsoft.Extensions.Primitives.StringValues value))
            return value;
        else
            return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
    }
}
