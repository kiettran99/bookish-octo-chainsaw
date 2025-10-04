using Common.Models;

namespace Portal.Domain.Interfaces.Infrastructures;

public interface IJwtService
{
    UserInfomationTokenModel? ValidateJwtToken(string token);
}
