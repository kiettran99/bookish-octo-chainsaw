using System.Security.Claims;
using Portal.Domain.Interfaces.Infrastructures;

namespace CineReview.API.Middlewares;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;

    public JwtMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IJwtService jwtService)
    {
        var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split("Bearer ").LastOrDefault();
        if (!string.IsNullOrEmpty(token))
        {
            var userInfoModel = jwtService.ValidateJwtToken(token);
            if (userInfoModel != null)
            {
                // Create ClaimsIdentity with user roles if available
                var claimsIdentity = new ClaimsIdentity();
                claimsIdentity.AddClaim(new Claim("id", Convert.ToString(userInfoModel.Id)));

                if (!string.IsNullOrEmpty(userInfoModel.FullName))
                    claimsIdentity.AddClaim(new Claim(ClaimTypes.GivenName, userInfoModel.FullName));

                if (userInfoModel.Roles?.Count > 0)
                    claimsIdentity.AddClaims(userInfoModel.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

                if (!string.IsNullOrEmpty(userInfoModel.ProviderAccountId))
                    claimsIdentity.AddClaim(new Claim("providerAccountId", userInfoModel.ProviderAccountId));

                claimsIdentity.AddClaim(new Claim("isBanned", Convert.ToString(userInfoModel.IsBanned)));

                // Create ClaimsPrincipal and set it to HttpContext.User
                var principal = new ClaimsPrincipal(claimsIdentity);
                context.User = principal;
            }
        }
        await _next(context);
    }
}