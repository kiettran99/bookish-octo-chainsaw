using System.Security.Claims;
using Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Identity.API.Attributes;

[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
public class AuthorizeAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>
    /// Roles require to use resource
    /// </summary>
    private readonly List<ERoles> _roles;

    public AuthorizeAttribute()
    {
        _roles = new List<ERoles>();
    }

    public AuthorizeAttribute(params ERoles[] roles)
    {
        _roles = roles.ToList();
        if (!_roles.Exists(r => r == ERoles.Administrator))
        {
            _roles.Add(ERoles.Administrator);
        }
    }
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // skip authorization if action is decorated with [AllowAnonymous] attribute
        var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any();
        if (allowAnonymous)
            return;

        // authorization
        var id = context.HttpContext.User.FindFirst("id")?.Value;
        var roles = context.HttpContext.User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
        var isBanned = context.HttpContext.User.FindFirst("isBanned")?.Value;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(isBanned) || "True".Equals(isBanned))
        {
            context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
        }
        else
        {
            // check role
            if (_roles.Count > 0)
            {
                if (roles?.Count > 0)
                {
                    var hasRole = _roles.Exists(r => roles.Contains(r.ToString()));
                    if (!hasRole)
                    {
                        context.Result = new JsonResult(new { message = "Forbidden" }) { StatusCode = StatusCodes.Status403Forbidden };
                    }
                }
                else
                {
                    context.Result = new JsonResult(new { message = "Forbidden" }) { StatusCode = StatusCodes.Status403Forbidden };
                }
            }
        }
    }
}