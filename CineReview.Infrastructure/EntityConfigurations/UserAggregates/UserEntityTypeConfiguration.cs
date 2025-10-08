using CineReview.Domain.AggregatesModel.UserAggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CineReview.Infrastructure.EntityConfigurations.UserAggregates;

public class UserEntityTypeConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(nameof(User));
        builder.HasKey(x => x.Id);

        builder.HasIndex(o => o.UserName).IsUnique();
        builder.HasIndex(o => o.Email).IsUnique();
        builder.HasIndex(o => o.ProviderAccountId).IsUnique();

        // Default value for CommunicationScore
        builder.Property(x => x.CommunicationScore).HasDefaultValue(0L);
    }
}
