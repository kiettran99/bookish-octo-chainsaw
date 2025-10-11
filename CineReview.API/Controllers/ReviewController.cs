using CineReview.API.Attributes;
using CineReview.Application.Interfaces.Infrastructures;
using CineReview.Domain.Models.ReviewModels;
using Common.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CineReview.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : CommonController
{
    private readonly IReviewService _reviewService;

    public ReviewController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// Create a new review for a movie
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequestModel request)
    {
        var userId = GetUserIdByToken();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var response = await _reviewService.CreateReviewAsync(request, userId.Value);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Update an existing review (Admin only)
    /// </summary>
    [HttpPut]
    [Authorize(ERoles.Administrator)]
    public async Task<IActionResult> UpdateReview([FromBody] UpdateReviewRequestModel request)
    {
        var response = await _reviewService.UpdateReviewAsync(request);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Delete a review (Admin only - soft delete, sets status to Deleted)
    /// </summary>
    [HttpDelete("{reviewId}")]
    [Authorize(ERoles.Administrator)]
    public async Task<IActionResult> DeleteReview(int reviewId, [FromQuery] string? rejectReason = null)
    {
        var response = await _reviewService.DeleteReviewAsync(reviewId, rejectReason);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Approve a pending review (Admin endpoint - sets status to Released)
    /// </summary>
    [HttpPost("{reviewId}/approve")]
    [Authorize]
    public async Task<IActionResult> ApproveReview(int reviewId)
    {
        var response = await _reviewService.ApproveReviewAsync(reviewId);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Get a specific review by ID
    /// </summary>
    [HttpGet("{reviewId}")]
    public async Task<IActionResult> GetReview(int reviewId)
    {
        var response = await _reviewService.GetReviewByIdAsync(reviewId);

        if (!response.IsSuccess)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Get reviews with optional filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReviews([FromQuery] ReviewListRequestModel request)
    {
        var response = await _reviewService.GetReviewsAsync(request);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Get reviews for a specific movie
    /// </summary>
    [HttpGet("movie/{tmdbMovieId}")]
    public async Task<IActionResult> GetMovieReviews(int tmdbMovieId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 10;
        }

        var request = new ReviewListRequestModel
        {
            TmdbMovieId = tmdbMovieId,
            Page = page,
            PageSize = pageSize
        };

        var response = await _reviewService.GetReviewsAsync(request);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Get reviews for the current logged-in user
    /// </summary>
    [HttpGet("my-reviews")]
    [Authorize]
    public async Task<IActionResult> GetMyReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userId = GetUserIdByToken();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 10;
        }

        var request = new ReviewListRequestModel
        {
            UserId = userId.Value,
            Page = page,
            PageSize = pageSize
        };

        var response = await _reviewService.GetReviewsAsync(request);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Check if current logged-in user has reviewed a specific movie (returns the review if exists)
    /// </summary>
    [HttpGet("my-review/movie/{tmdbMovieId}")]
    [Authorize]
    public async Task<IActionResult> GetMyMovieReview(int tmdbMovieId)
    {
        var userId = GetUserIdByToken();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var request = new ReviewListRequestModel
        {
            UserId = userId.Value,
            TmdbMovieId = tmdbMovieId,
            Page = 1,
            PageSize = 10
        };

        var response = await _reviewService.GetReviewsAsync(request);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(new
        {
            isSuccess = true,
            data = response.Data?.Items ?? Array.Empty<ReviewResponseModel>()
        });
    }

    /// <summary>
    /// Rate a review as fair or unfair
    /// </summary>
    [HttpPost("rate")]
    [Authorize]
    public async Task<IActionResult> RateReview([FromBody] RateReviewRequestModel request)
    {
        var userId = GetUserIdByToken();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var response = await _reviewService.RateReviewAsync(request, userId.Value);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Recalculate communication score for a review (Admin endpoint)
    /// </summary>
    [HttpPost("{reviewId}/recalculate-score")]
    [Authorize]
    public async Task<IActionResult> RecalculateCommunicationScore(int reviewId)
    {
        var response = await _reviewService.RecalculateCommunicationScoreAsync(reviewId);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Get batch ratings status for multiple reviews for the current user
    /// </summary>
    [HttpGet("batch-ratings")]
    [Authorize]
    public async Task<IActionResult> GetBatchRatings([FromQuery] string reviewIds)
    {
        var userId = GetUserIdByToken();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(reviewIds))
        {
            return Ok(new
            {
                isSuccess = true,
                data = new { ratings = new Dictionary<int, object>() }
            });
        }

        try
        {
            var ids = reviewIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.Parse(id.Trim()))
                .ToList();

            var response = await _reviewService.GetBatchRatingsForUserAsync(ids, userId.Value);

            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (FormatException)
        {
            return BadRequest(new { isSuccess = false, message = "Invalid review IDs format" });
        }
    }
}
