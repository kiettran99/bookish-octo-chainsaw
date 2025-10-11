using CineReview.Domain.Models.ReviewModels;
using Common.Models;

namespace CineReview.Application.Interfaces.Infrastructures;

public interface IReviewService
{
    Task<ServiceResponse<ReviewResponseModel>> CreateReviewAsync(CreateReviewRequestModel request, int userId);
    Task<ServiceResponse<ReviewResponseModel>> UpdateReviewAsync(UpdateReviewRequestModel request);
    Task<ServiceResponse<bool>> DeleteReviewAsync(int reviewId, string? rejectReason);
    Task<ServiceResponse<bool>> ApproveReviewAsync(int reviewId);
    Task<ServiceResponse<ReviewResponseModel>> GetReviewByIdAsync(int reviewId);
    Task<ServiceResponse<PagedResult<ReviewResponseModel>>> GetReviewsAsync(ReviewListRequestModel request);
    Task<ServiceResponse<PagedResult<ReviewResponseModel>>> GetAdminReviewsAsync(ReviewListRequestModel request);
    Task<ServiceResponse<bool>> RateReviewAsync(RateReviewRequestModel request, int userId);
    Task<ServiceResponse<bool>> RecalculateCommunicationScoreAsync(int reviewId);
    Task<ServiceResponse<BatchRatingsResponseModel>> GetBatchRatingsForUserAsync(List<int> reviewIds, int userId);
    Task<ServiceResponse<PagedResult<ReviewResponseModel>>> GetMyReviewsAsync(ReviewListRequestModel request);
}
