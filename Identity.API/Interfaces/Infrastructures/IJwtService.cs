using Common.Models;
using Identity.Domain.AggregatesModel.UserAggregates;

namespace Identity.Domain.Interfaces.Infrastructures;

public interface IJwtService
{
    string GenerateJwtToken(User user, int expirationInMinutes = 60);
    UserInfomationTokenModel? ValidateJwtToken(string token);
}
