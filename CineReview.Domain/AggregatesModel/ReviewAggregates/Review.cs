using System.ComponentModel.DataAnnotations.Schema;
using CineReview.Domain.Enums;
using Common.SeedWork;

namespace CineReview.Domain.AggregatesModel.ReviewAggregates;

public class Review : Entity
{
    public int UserId { get; set; }

    public int TmdbMovieId { get; set; }

    public ReviewStatus Status { get; set; }

    public long CommunicationScore { get; set; }

    public ReviewType Type { get; set; }

    [Column(TypeName = "TEXT")]
    public string? DescriptionTag { get; set; } // JSON string array for tags

    [Column(TypeName = "TEXT")]
    public string? Description { get; set; } // Manual content for normal reviews

    public double Rating { get; set; } // 1-10 scale (supports decimal values)

    [Column(TypeName = "TEXT")]
    public string? RejectReason { get; set; } // Reason when admin sets status to Deleted

    // Navigation properties
    public virtual ICollection<UserRating> UserRatings { get; set; } = new List<UserRating>();
}
