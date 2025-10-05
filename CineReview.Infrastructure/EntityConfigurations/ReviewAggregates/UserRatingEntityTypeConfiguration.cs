using CineReview.Domain.AggregatesModel.ReviewAggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CineReview.Infrastructure.EntityConfigurations.ReviewAggregates;

public class UserRatingEntityTypeConfiguration : IEntityTypeConfiguration<UserRating>
{
    public void Configure(EntityTypeBuilder<UserRating> builder)
    {
        builder.ToTable(nameof(UserRating));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.ReviewId).IsRequired();
        builder.Property(x => x.RatingType).IsRequired();

        // Indexes
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ReviewId);
        builder.HasIndex(x => new { x.UserId, x.ReviewId }).IsUnique(); // User can only rate a review once
    }
}
