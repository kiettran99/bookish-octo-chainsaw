namespace Portal.Domain.Models.UserModels;

public class UserReviewStatsResponseModel
{
    public int TotalReviews { get; set; }
    public int FairReviews { get; set; }
    public int UnfairReviews { get; set; }
}
