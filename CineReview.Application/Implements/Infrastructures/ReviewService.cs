using System.Text.Json;
using CineReview.Application.Interfaces.Infrastructures;
using CineReview.Domain.AggregatesModel.ReviewAggregates;
using CineReview.Domain.AggregatesModel.UserAggregates;
using CineReview.Domain.Enums;
using CineReview.Domain.Models.ReviewModels;
using Common.Models;
using Common.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace CineReview.Application.Implements.Infrastructures;

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReviewService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResponse<ReviewResponseModel>> CreateReviewAsync(CreateReviewRequestModel request, int userId)
    {
        try
        {
            // Validate rating
            if (request.Rating < 1 || request.Rating > 10)
            {
                return new ServiceResponse<ReviewResponseModel>("Rating must be between 1 and 10");
            }

            // Validate content based on type
            if (request.Type == ReviewType.Tag && (request.DescriptionTag == null || !request.DescriptionTag.Any()))
            {
                return new ServiceResponse<ReviewResponseModel>("Tag review must have at least one tag");
            }

            if (request.Type == ReviewType.Normal && string.IsNullOrWhiteSpace(request.Description))
            {
                return new ServiceResponse<ReviewResponseModel>("Normal review must have description");
            }

            // Check if user already reviewed this movie
            var existingReview = await _unitOfWork.Repository<Review>().GetQueryable()
                .Where(r => r.UserId == userId && r.TmdbMovieId == request.TmdbMovieId && r.Status != ReviewStatus.Deleted)
                .FirstOrDefaultAsync();

            if (existingReview != null)
            {
                return new ServiceResponse<ReviewResponseModel>("You have already reviewed this movie");
            }

            var review = new Review
            {
                UserId = userId,
                TmdbMovieId = request.TmdbMovieId,
                Status = ReviewStatus.Pending,
                CommunicationScore = 0,
                Type = request.Type,
                DescriptionTag = request.Type == ReviewType.Tag ? JsonSerializer.Serialize(request.DescriptionTag) : null,
                Description = request.Type == ReviewType.Normal ? request.Description : null,
                Rating = request.Rating
            };

            _unitOfWork.Repository<Review>().Add(review);
            await _unitOfWork.SaveChangesAsync();

            var response = await GetReviewByIdAsync(review.Id);
            return response;
        }
        catch (Exception ex)
        {
            return new ServiceResponse<ReviewResponseModel>(ex.Message);
        }
    }

    public async Task<ServiceResponse<ReviewResponseModel>> UpdateReviewAsync(UpdateReviewRequestModel request, int userId)
    {
        try
        {
            var review = await _unitOfWork.Repository<Review>().GetQueryable()
                .FirstOrDefaultAsync(r => r.Id == request.ReviewId && r.UserId == userId);

            if (review == null)
            {
                return new ServiceResponse<ReviewResponseModel>("Review not found or you don't have permission to update");
            }

            if (review.Status == ReviewStatus.Deleted)
            {
                return new ServiceResponse<ReviewResponseModel>("Cannot update deleted review");
            }

            // Validate rating
            if (request.Rating < 1 || request.Rating > 10)
            {
                return new ServiceResponse<ReviewResponseModel>("Rating must be between 1 and 10");
            }

            // Validate content based on type
            if (request.Type == ReviewType.Tag && (request.DescriptionTag == null || !request.DescriptionTag.Any()))
            {
                return new ServiceResponse<ReviewResponseModel>("Tag review must have at least one tag");
            }

            if (request.Type == ReviewType.Normal && string.IsNullOrWhiteSpace(request.Description))
            {
                return new ServiceResponse<ReviewResponseModel>("Normal review must have description");
            }

            review.Type = request.Type;
            review.DescriptionTag = request.Type == ReviewType.Tag ? JsonSerializer.Serialize(request.DescriptionTag) : null;
            review.Description = request.Type == ReviewType.Normal ? request.Description : null;
            review.Rating = request.Rating;
            review.UpdatedOnUtc = DateTime.UtcNow;

            _unitOfWork.Repository<Review>().Update(review);
            await _unitOfWork.SaveChangesAsync();

            var response = await GetReviewByIdAsync(review.Id);
            return response;
        }
        catch (Exception ex)
        {
            return new ServiceResponse<ReviewResponseModel>(ex.Message);
        }
    }

    public async Task<ServiceResponse<bool>> DeleteReviewAsync(int reviewId, int userId)
    {
        try
        {
            var review = await _unitOfWork.Repository<Review>().GetQueryable()
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);

            if (review == null)
            {
                return new ServiceResponse<bool>("Review not found or you don't have permission to delete");
            }

            review.Status = ReviewStatus.Deleted;
            review.UpdatedOnUtc = DateTime.UtcNow;

            _unitOfWork.Repository<Review>().Update(review);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResponse<bool>(true);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<bool>(ex.Message);
        }
    }

    public async Task<ServiceResponse<ReviewResponseModel>> GetReviewByIdAsync(int reviewId)
    {
        try
        {
            var review = await _unitOfWork.Repository<Review>().GetQueryable()
                .Where(r => r.Id == reviewId)
                .Select(r => new
                {
                    Review = r,
                    User = _unitOfWork.Repository<User>().GetQueryable().FirstOrDefault(u => u.Id == r.UserId),
                    FairVotes = r.UserRatings.Count(ur => ur.RatingType == RatingType.Fair),
                    UnfairVotes = r.UserRatings.Count(ur => ur.RatingType == RatingType.Unfair)
                })
                .FirstOrDefaultAsync();

            if (review == null)
            {
                return new ServiceResponse<ReviewResponseModel>("Review not found");
            }

            var response = new ReviewResponseModel
            {
                Id = review.Review.Id,
                UserId = review.Review.UserId,
                UserName = review.User?.UserName,
                UserAvatar = review.User?.Avatar,
                TmdbMovieId = review.Review.TmdbMovieId,
                Status = review.Review.Status,
                CommunicationScore = review.Review.CommunicationScore,
                Type = review.Review.Type,
                DescriptionTag = review.Review.Type == ReviewType.Tag && !string.IsNullOrEmpty(review.Review.DescriptionTag)
                    ? JsonSerializer.Deserialize<List<string>>(review.Review.DescriptionTag)
                    : null,
                Description = review.Review.Description,
                Rating = review.Review.Rating,
                FairVotes = review.FairVotes,
                UnfairVotes = review.UnfairVotes,
                CreatedOnUtc = review.Review.CreatedOnUtc,
                UpdatedOnUtc = review.Review.UpdatedOnUtc
            };

            return new ServiceResponse<ReviewResponseModel>(response);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<ReviewResponseModel>(ex.Message);
        }
    }

    public async Task<ServiceResponse<List<ReviewResponseModel>>> GetReviewsAsync(ReviewListRequestModel request)
    {
        try
        {
            var query = _unitOfWork.Repository<Review>().GetQueryable();

            if (request.TmdbMovieId.HasValue)
            {
                query = query.Where(r => r.TmdbMovieId == request.TmdbMovieId.Value);
            }

            if (request.UserId.HasValue)
            {
                query = query.Where(r => r.UserId == request.UserId.Value);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(r => r.Status == request.Status.Value);
            }
            else
            {
                // By default, don't show deleted reviews
                query = query.Where(r => r.Status != ReviewStatus.Deleted);
            }

            var reviews = await query
                .OrderByDescending(r => r.CreatedOnUtc)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(r => new
                {
                    Review = r,
                    User = _unitOfWork.Repository<User>().GetQueryable().FirstOrDefault(u => u.Id == r.UserId),
                    FairVotes = r.UserRatings.Count(ur => ur.RatingType == RatingType.Fair),
                    UnfairVotes = r.UserRatings.Count(ur => ur.RatingType == RatingType.Unfair)
                })
                .ToListAsync();

            var response = reviews.Select(r => new ReviewResponseModel
            {
                Id = r.Review.Id,
                UserId = r.Review.UserId,
                UserName = r.User?.UserName,
                UserAvatar = r.User?.Avatar,
                TmdbMovieId = r.Review.TmdbMovieId,
                Status = r.Review.Status,
                CommunicationScore = r.Review.CommunicationScore,
                Type = r.Review.Type,
                DescriptionTag = r.Review.Type == ReviewType.Tag && !string.IsNullOrEmpty(r.Review.DescriptionTag)
                    ? JsonSerializer.Deserialize<List<string>>(r.Review.DescriptionTag)
                    : null,
                Description = r.Review.Description,
                Rating = r.Review.Rating,
                FairVotes = r.FairVotes,
                UnfairVotes = r.UnfairVotes,
                CreatedOnUtc = r.Review.CreatedOnUtc,
                UpdatedOnUtc = r.Review.UpdatedOnUtc
            }).ToList();

            return new ServiceResponse<List<ReviewResponseModel>>(response);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<List<ReviewResponseModel>>(ex.Message);
        }
    }

    public async Task<ServiceResponse<bool>> RateReviewAsync(RateReviewRequestModel request, int userId)
    {
        try
        {
            var review = await _unitOfWork.Repository<Review>().GetQueryable()
                .FirstOrDefaultAsync(r => r.Id == request.ReviewId);

            if (review == null)
            {
                return new ServiceResponse<bool>("Review not found");
            }

            if (review.UserId == userId)
            {
                return new ServiceResponse<bool>("You cannot rate your own review");
            }

            // Check if user already rated this review
            var existingRating = await _unitOfWork.Repository<UserRating>().GetQueryable()
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.ReviewId == request.ReviewId);

            if (existingRating != null)
            {
                // Update existing rating
                existingRating.RatingType = request.RatingType;
                existingRating.UpdatedOnUtc = DateTime.UtcNow;
                _unitOfWork.Repository<UserRating>().Update(existingRating);
            }
            else
            {
                // Create new rating
                var rating = new UserRating
                {
                    UserId = userId,
                    ReviewId = request.ReviewId,
                    RatingType = request.RatingType
                };
                _unitOfWork.Repository<UserRating>().Add(rating);
            }

            await _unitOfWork.SaveChangesAsync();

            // Recalculate communication score
            await RecalculateCommunicationScoreAsync(request.ReviewId);

            return new ServiceResponse<bool>(true);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<bool>(ex.Message);
        }
    }

    public async Task<ServiceResponse<bool>> RecalculateCommunicationScoreAsync(int reviewId)
    {
        try
        {
            var review = await _unitOfWork.Repository<Review>().GetQueryable()
                .Include(r => r.UserRatings)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null)
            {
                return new ServiceResponse<bool>("Review not found");
            }

            var fairVotes = review.UserRatings.Count(ur => ur.RatingType == RatingType.Fair);
            var unfairVotes = review.UserRatings.Count(ur => ur.RatingType == RatingType.Unfair);
            var totalVotes = fairVotes + unfairVotes;

            // Calculate communication score as percentage of fair votes
            // Formula: (fairVotes / totalVotes) * 100
            // If no votes, score is 0
            review.CommunicationScore = totalVotes > 0 ? (double)fairVotes / totalVotes * 100 : 0;
            review.UpdatedOnUtc = DateTime.UtcNow;

            _unitOfWork.Repository<Review>().Update(review);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResponse<bool>(true);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<bool>(ex.Message);
        }
    }
}
