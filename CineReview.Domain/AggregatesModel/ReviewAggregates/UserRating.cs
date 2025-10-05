using CineReview.Domain.Enums;
using Common.SeedWork;

namespace CineReview.Domain.AggregatesModel.ReviewAggregates;

public class UserRating : Entity
{
    public int UserId { get; set; } // User who rates the review

    public int ReviewId { get; set; }

    public RatingType RatingType { get; set; } // Fair or Unfair

    // Navigation properties
    public virtual Review Review { get; set; } = null!;
}
