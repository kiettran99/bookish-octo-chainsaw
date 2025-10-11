using CineReview.Domain.AggregatesModel.ReviewAggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CineReview.Infrastructure.EntityConfigurations.ReviewAggregates;

public class ReviewEntityTypeConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable(nameof(Review));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.TmdbMovieId).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CommunicationScore).HasDefaultValue(0);
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Rating).IsRequired().HasColumnType("REAL"); // SQLite REAL type for double

        // Indexes
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TmdbMovieId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.UserId, x.TmdbMovieId });

        // Relationship with UserRating
        builder.HasMany(x => x.UserRatings)
               .WithOne(x => x.Review)
               .HasForeignKey(x => x.ReviewId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
