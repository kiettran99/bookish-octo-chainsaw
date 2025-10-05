using Common.Models;
using Identity.Domain.Models.Authenticates;

namespace Identity.Domain.Interfaces.Services;

public interface IAccountService
{
    Task<ServiceResponse<AuthenticateResponse>> ClientAuthenticateAsync(ClientAuthenticateRequest model);
    Task<ServiceResponse<AuthenticateResponse>> GoogleAuthenticateAsync(GoogleAuthenticateRequest model);
}
