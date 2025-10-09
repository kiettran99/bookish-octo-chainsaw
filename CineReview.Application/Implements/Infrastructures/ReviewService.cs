using System.Text.Json;
using CineReview.Application.Interfaces.Infrastructures;
using CineReview.Domain.AggregatesModel.ReviewAggregates;
using CineReview.Domain.AggregatesModel.UserAggregates;
using CineReview.Domain.Enums;
using CineReview.Domain.Models.ReviewModels;
using Common.Models;
using Common.SeedWork;
using Microsoft.EntityFrameworkCore;
using Hangfire;

namespace CineReview.Application.Implements.Infrastructures;

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICommunicationScoreService _communicationScoreService;

    public ReviewService(IUnitOfWork unitOfWork, ICommunicationScoreService communicationScoreService)
    {
        _unitOfWork = unitOfWork;
        _communicationScoreService = communicationScoreService;
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
            if (request.Type == ReviewType.Tag && request.DescriptionTag == null)
            {
                return new ServiceResponse<ReviewResponseModel>("Tag review must have tag data");
            }

            if (request.Type == ReviewType.Normal && string.IsNullOrWhiteSpace(request.Description))
            {
                return new ServiceResponse<ReviewResponseModel>("Normal review must have description");
            }

            // Check if user already reviewed this movie (limit one per type)
            var existingReviews = await _unitOfWork.Repository<Review>().GetQueryable()
                .Where(r => r.UserId == userId && r.TmdbMovieId == request.TmdbMovieId && r.Status != ReviewStatus.Deleted)
                .ToListAsync();

            if (existingReviews.Count >= 2)
            {
                return new ServiceResponse<ReviewResponseModel>("You have reached the review limit for this movie");
            }

            if (existingReviews.Any(r => r.Type == request.Type))
            {
                return new ServiceResponse<ReviewResponseModel>("You have already submitted this type of review for this movie");
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
            if (request.Type == ReviewType.Tag && request.DescriptionTag == null)
            {
                return new ServiceResponse<ReviewResponseModel>("Tag review must have tag data");
            }

            if (request.Type == ReviewType.Normal && string.IsNullOrWhiteSpace(request.Description))
            {
                return new ServiceResponse<ReviewResponseModel>("Normal review must have description");
            }

            if (review.Type != request.Type)
            {
                var duplicateTypeExists = await _unitOfWork.Repository<Review>().GetQueryable()
                    .AnyAsync(r => r.UserId == userId
                                   && r.TmdbMovieId == review.TmdbMovieId
                                   && r.Id != review.Id
                                   && r.Status != ReviewStatus.Deleted
                                   && r.Type == request.Type);

                if (duplicateTypeExists)
                {
                    return new ServiceResponse<ReviewResponseModel>("You have already submitted this type of review for this movie");
                }
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
                    User = _unitOfWork.Repository<User>().GetQueryable().FirstOrDefault(u => u.Id == r.UserId)
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
                UserFullName = review.User?.FullName,
                UserAvatar = review.User?.Avatar,
                UserCommunicationScore = review.User?.CommunicationScore ?? 0,
                TmdbMovieId = review.Review.TmdbMovieId,
                Status = review.Review.Status,
                CommunicationScore = review.Review.CommunicationScore,
                Type = review.Review.Type,
                DescriptionTag = review.Review.Type == ReviewType.Tag && !string.IsNullOrEmpty(review.Review.DescriptionTag)
                    ? JsonSerializer.Deserialize<object>(review.Review.DescriptionTag)
                    : null,
                Description = review.Review.Description,
                Rating = review.Review.Rating,
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

    public async Task<ServiceResponse<PagedResult<ReviewResponseModel>>> GetReviewsAsync(ReviewListRequestModel request)
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
                
                // Also filter out Normal (Freeform) reviews that are Pending
                // Tag reviews can be shown even if Pending
                query = query.Where(r => 
                    r.Type == ReviewType.Tag || 
                    (r.Type == ReviewType.Normal && r.Status == ReviewStatus.Released));
            }

            var page = request.Page < 1 ? 1 : request.Page;
            var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

            var totalCount = await query.CountAsync();

            var reviews = await query
                .OrderByDescending(r => r.CreatedOnUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    Review = r,
                    User = _unitOfWork.Repository<User>().GetQueryable().FirstOrDefault(u => u.Id == r.UserId)
                })
                .ToListAsync();

            var response = reviews.Select(r => new ReviewResponseModel
            {
                Id = r.Review.Id,
                UserId = r.Review.UserId,
                UserName = r.User?.UserName,
                UserFullName = r.User?.FullName,
                UserAvatar = r.User?.Avatar,
                UserCommunicationScore = r.User?.CommunicationScore ?? 0,
                TmdbMovieId = r.Review.TmdbMovieId,
                Status = r.Review.Status,
                CommunicationScore = r.Review.CommunicationScore,
                Type = r.Review.Type,
                DescriptionTag = r.Review.Type == ReviewType.Tag && !string.IsNullOrEmpty(r.Review.DescriptionTag)
                    ? JsonSerializer.Deserialize<object>(r.Review.DescriptionTag)
                    : null,
                Description = r.Review.Description,
                Rating = r.Review.Rating,
                CreatedOnUtc = r.Review.CreatedOnUtc,
                UpdatedOnUtc = r.Review.UpdatedOnUtc
            }).ToList();

            var pagedResult = new PagedResult<ReviewResponseModel>(response, page, pageSize, totalCount);

            return new ServiceResponse<PagedResult<ReviewResponseModel>>(pagedResult);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<PagedResult<ReviewResponseModel>>(ex.Message);
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

            int? previousRatingType = existingRating?.RatingType == null ? null : (int)existingRating.RatingType;

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

            // Enqueue Hangfire background job to update communication score
            BackgroundJob.Enqueue(() => _communicationScoreService.UpdateCommunicationScoreAsync(
                review.UserId, 
                request.ReviewId, 
                previousRatingType, 
                (int)request.RatingType));

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
            // This method is deprecated and kept only for backward compatibility
            // Communication scores are now updated via Hangfire background jobs
            // when users vote on reviews (Fair/Unfair)
            
            var review = await _unitOfWork.Repository<Review>().GetQueryable()
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null)
            {
                return new ServiceResponse<bool>("Review not found");
            }

            // No-op: Score is maintained by background jobs
            return new ServiceResponse<bool>(true);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<bool>(ex.Message);
        }
    }

    public async Task<ServiceResponse<BatchRatingsResponseModel>> GetBatchRatingsForUserAsync(List<int> reviewIds, int userId)
    {
        try
        {
            if (reviewIds == null || reviewIds.Count == 0)
            {
                return new ServiceResponse<BatchRatingsResponseModel>(new BatchRatingsResponseModel());
            }

            // Get all user ratings for the specified review IDs
            var userRatings = await _unitOfWork.Repository<UserRating>().GetQueryable()
                .Where(ur => ur.UserId == userId && reviewIds.Contains(ur.ReviewId))
                .ToListAsync();

            var response = new BatchRatingsResponseModel();

            foreach (var reviewId in reviewIds)
            {
                var rating = userRatings.FirstOrDefault(ur => ur.ReviewId == reviewId);
                response.Ratings[reviewId] = new ReviewRatingStatusModel
                {
                    ReviewId = reviewId,
                    HasRated = rating != null,
                    RatingType = rating?.RatingType
                };
            }

            return new ServiceResponse<BatchRatingsResponseModel>(response);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<BatchRatingsResponseModel>(ex.Message);
        }
    }
}
