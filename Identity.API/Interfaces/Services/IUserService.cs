using Common.Models;
using Identity.Domain.Models.Users;

namespace Identity.Domain.Interfaces.Services;

public interface IUserService
{
    Task<ServiceResponse<bool>> UpdateAsync(int id, UserUpdateRequestModel userModel);
    Task<ServiceResponse<PagingCommonResponse<UserPagingModel>>> GetPagingAsync(UserPagingRequestModel request);
    Task<ServiceResponse<List<UserPagingModel>>> GetPartnersAsync();
}
