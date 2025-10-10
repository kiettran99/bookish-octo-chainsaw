using System.Threading;
using System.Threading.Tasks;

namespace CineReview.Client.Features.Users;

public interface IUserProfileDataProvider
{
    Task<UserProfilePageViewModel> GetProfileAsync(string userName, int page, CancellationToken cancellationToken = default);
}
