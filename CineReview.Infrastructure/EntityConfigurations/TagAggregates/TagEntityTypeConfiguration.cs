using CineReview.Domain.AggregatesModel.TagAggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CineReview.Infrastructure.EntityConfigurations.TagAggregates;

public class TagEntityTypeConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable(nameof(Tag));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Category).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.DisplayOrder).IsRequired().HasDefaultValue(0);

        // Indexes
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.Category, x.DisplayOrder });
    }
}
