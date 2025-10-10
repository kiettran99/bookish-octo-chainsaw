namespace Portal.Domain.Models.UserModels;

public class UserReviewStatsResponseModel
{
    public int TotalReviews { get; set; }
    public int ReleasedReviews { get; set; }
    public int PendingReviews { get; set; }
    public int TagReviews { get; set; }
    public int FreeformReviews { get; set; }
    public double? AverageRating { get; set; }
}
