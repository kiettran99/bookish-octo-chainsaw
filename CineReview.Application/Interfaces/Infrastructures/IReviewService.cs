using CineReview.Domain.Models.ReviewModels;
using Common.Models;

namespace CineReview.Application.Interfaces.Infrastructures;

public interface IReviewService
{
    Task<ServiceResponse<ReviewResponseModel>> CreateReviewAsync(CreateReviewRequestModel request, int userId);
    Task<ServiceResponse<ReviewResponseModel>> UpdateReviewAsync(UpdateReviewRequestModel request, int userId);
    Task<ServiceResponse<bool>> DeleteReviewAsync(int reviewId, int userId);
    Task<ServiceResponse<ReviewResponseModel>> GetReviewByIdAsync(int reviewId);
    Task<ServiceResponse<List<ReviewResponseModel>>> GetReviewsAsync(ReviewListRequestModel request);
    Task<ServiceResponse<bool>> RateReviewAsync(RateReviewRequestModel request, int userId);
    Task<ServiceResponse<bool>> RecalculateCommunicationScoreAsync(int reviewId);
}
