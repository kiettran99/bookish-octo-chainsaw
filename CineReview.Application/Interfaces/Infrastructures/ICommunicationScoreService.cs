namespace CineReview.Application.Interfaces.Infrastructures;

public interface ICommunicationScoreService
{
    /// <summary>
    /// Update user communication score when a review is rated
    /// This method is designed to be called by Hangfire background job
    /// </summary>
    /// <param name="reviewOwnerId">User ID who owns the review</param>
    /// <param name="reviewId">Review ID being rated</param>
    /// <param name="previousRatingType">Previous rating type (null if first time rating)</param>
    /// <param name="newRatingType">New rating type</param>
    Task UpdateCommunicationScoreAsync(int reviewOwnerId, int reviewId, int? previousRatingType, int newRatingType);
}
