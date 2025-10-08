using CineReview.Domain.Enums;
using System.Text.Json;

namespace CineReview.Domain.Models.ReviewModels;

public class CreateReviewRequestModel
{
    public int TmdbMovieId { get; set; }
    public ReviewType Type { get; set; }
    public object? DescriptionTag { get; set; } // Can be List<string> or List<TagRatingItem> or any JSON structure
    public string? Description { get; set; }
    public int Rating { get; set; } // 1-10 scale
}

public class UpdateReviewRequestModel
{
    public int ReviewId { get; set; }
    public ReviewType Type { get; set; }
    public object? DescriptionTag { get; set; } // Can be List<string> or List<TagRatingItem> or any JSON structure
    public string? Description { get; set; }
    public int Rating { get; set; }
}

public class TagRatingItem
{
    public int TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public int Rating { get; set; }
}

public class RateReviewRequestModel
{
    public int ReviewId { get; set; }
    public RatingType RatingType { get; set; }
}

public class ReviewResponseModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserAvatar { get; set; }
    public long UserCommunicationScore { get; set; }
    public int TmdbMovieId { get; set; }
    public ReviewStatus Status { get; set; }
    public long CommunicationScore { get; set; }
    public ReviewType Type { get; set; }
    public object? DescriptionTag { get; set; } // Can be List<string> or List<TagRatingItem> or any JSON structure
    public string? Description { get; set; }
    public int Rating { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}

public class ReviewListRequestModel
{
    public int? TmdbMovieId { get; set; }
    public int? UserId { get; set; }
    public ReviewStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ReviewRatingStatusModel
{
    public int ReviewId { get; set; }
    public bool HasRated { get; set; }
    public RatingType? RatingType { get; set; }
}

public class BatchRatingsResponseModel
{
    public Dictionary<int, ReviewRatingStatusModel> Ratings { get; set; } = new();
}
