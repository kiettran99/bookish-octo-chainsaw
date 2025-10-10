using CineReview.Application.Interfaces.Infrastructures;
using CineReview.Domain.Enums;
using Common.SeedWork;
using Microsoft.Extensions.Logging;

namespace CineReview.Application.Implements.Infrastructures;

public class CommunicationScoreService : ICommunicationScoreService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CommunicationScoreService> _logger;

    public CommunicationScoreService(IUnitOfWork unitOfWork, ILogger<CommunicationScoreService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task UpdateCommunicationScoreAsync(int reviewOwnerId, int reviewId, int? previousRatingType, int newRatingType)
    {
        try
        {
            _logger.LogInformation(
                "Updating communication score for user {UserId}, review {ReviewId}, previousRating: {PreviousRating}, newRating: {NewRating}",
                reviewOwnerId, reviewId, previousRatingType, newRatingType);

            // Calculate score change
            long scoreChange = 0;

            // Previous rating impact (reverse it)
            if (previousRatingType.HasValue)
            {
                scoreChange -= previousRatingType.Value == (int)RatingType.Fair ? 1 : -1;
            }

            // New rating impact
            scoreChange += newRatingType == (int)RatingType.Fair ? 1 : -1;

            if (scoreChange == 0)
            {
                _logger.LogInformation("No score change needed");
                return;
            }

            // Use raw SQL for atomic update to avoid race conditions
            var updatedOn = DateTime.UtcNow;

            // Update User CommunicationScore
            var userParameters = new Dictionary<string, object?>
            {
                { "scoreChange", scoreChange },
                { "updatedOn", updatedOn },
                { "userId", reviewOwnerId }
            };

            var userSql = @"
                UPDATE User 
                SET CommunicationScore = CommunicationScore + @scoreChange,
                    UpdatedOnUtc = @updatedOn
                WHERE Id = @userId";

            await _unitOfWork.ExecuteAsync(userSql, userParameters, System.Data.CommandType.Text);

            // Update Review CommunicationScore
            var reviewParameters = new Dictionary<string, object?>
            {
                { "scoreChange", scoreChange },
                { "updatedOn", updatedOn },
                { "reviewId", reviewId }
            };

            var reviewSql = @"
                UPDATE Review 
                SET CommunicationScore = CommunicationScore + @scoreChange,
                    UpdatedOnUtc = @updatedOn
                WHERE Id = @reviewId";

            await _unitOfWork.ExecuteAsync(reviewSql, reviewParameters, System.Data.CommandType.Text);

            _logger.LogInformation(
                "Successfully updated communication score for user {UserId} by {ScoreChange}",
                reviewOwnerId, scoreChange);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating communication score for user {UserId}, review {ReviewId}",
                reviewOwnerId, reviewId);
            throw;
        }
    }
}
