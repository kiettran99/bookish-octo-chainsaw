using CineReview.API.Attributes;
using CineReview.Application.Interfaces.Infrastructures;
using CineReview.Domain.Models.ReviewModels;
using Common.Models;
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
    /// Update an existing review
    /// </summary>
    [HttpPut]
    [Authorize]
    public async Task<IActionResult> UpdateReview([FromBody] UpdateReviewRequestModel request)
    {
        var userId = GetUserIdByToken();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var response = await _reviewService.UpdateReviewAsync(request, userId.Value);
        
        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Delete a review (soft delete - sets status to Deleted)
    /// </summary>
    [HttpDelete("{reviewId}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(int reviewId)
    {
        var userId = GetUserIdByToken();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var response = await _reviewService.DeleteReviewAsync(reviewId, userId.Value);
        
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
    /// Get reviews by a specific user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserReviews(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var request = new ReviewListRequestModel
        {
            UserId = userId,
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
    /// Check if user has reviewed a specific movie (returns the review if exists)
    /// </summary>
    [HttpGet("user/{userId}/movie/{tmdbMovieId}")]
    public async Task<IActionResult> GetUserMovieReview(int userId, int tmdbMovieId)
    {
        var request = new ReviewListRequestModel
        {
            UserId = userId,
            TmdbMovieId = tmdbMovieId,
            Page = 1,
            PageSize = 1
        };

        var response = await _reviewService.GetReviewsAsync(request);
        
        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        // Return null if no review found
        if (response.Data == null || !response.Data.Any())
        {
            return Ok(new
            {
                isSuccess = true,
                data = (ReviewResponseModel?)null
            });
        }

        // Return the first (and should be only) review
        return Ok(new
        {
            isSuccess = true,
            data = response.Data.First()
        });
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
            PageSize = 1
        };

        var response = await _reviewService.GetReviewsAsync(request);
        
        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        // Return null if no review found
        if (response.Data == null || !response.Data.Any())
        {
            return Ok(new
            {
                isSuccess = true,
                data = (ReviewResponseModel?)null
            });
        }

        // Return the first (and should be only) review
        return Ok(new
        {
            isSuccess = true,
            data = response.Data.First()
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
}
