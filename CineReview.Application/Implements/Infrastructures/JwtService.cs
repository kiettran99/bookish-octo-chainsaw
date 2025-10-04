using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Common.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Portal.Domain.Interfaces.Infrastructures;

namespace Portal.Infrastructure.Implements.Infrastructures;

public class JwtService : IJwtService
{
    private readonly AppSettings _appSettings;

    public JwtService(IOptions<AppSettings> appSettings)
    {
        _appSettings = appSettings.Value;
    }

    public UserInfomationTokenModel? ValidateJwtToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                // set clockskew to zero so tokens expire exactly at token expiration time (instead of 5 minutes later)
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = jwtToken.Claims.First(x => x.Type == "id").Value;
            var fullName = jwtToken.Claims.First(x => x.Type == "given_name").Value;
            var roles = jwtToken.Claims.Where(x => x.Type == "role").Select(x => x.Value).ToList();
            var providerAccountId = jwtToken.Claims.FirstOrDefault(x => x.Type == "providerAccountId")?.Value;
            var isBanned = jwtToken.Claims.FirstOrDefault(x => x.Type == "isBanned")?.Value;

            // return user id from JWT token if validation successful
            var userInfomationTokenModel = new UserInfomationTokenModel
            {
                Id = Convert.ToInt32(userId),
                FullName = fullName,
                Roles = roles,
                ProviderAccountId = providerAccountId,
                IsBanned = Convert.ToBoolean(isBanned)
            };
            return userInfomationTokenModel;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            // return null if validation fails
            return null;
        }
    }
}