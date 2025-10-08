using System.ComponentModel.DataAnnotations.Schema;
using Common.Enums;
using Common.SeedWork;

namespace CineReview.Domain.AggregatesModel.UserAggregates;

public class User : Entity
{
    [Column(TypeName = "varchar(100)")]
    public string UserName { get; set; } = null!;

    [Column(TypeName = "varchar(100)")]
    public string Email { get; set; } = null!;

    [Column(TypeName = "nvarchar(250)")]
    public string? FullName { get; set; }

    [Column(TypeName = "varchar(200)")]
    public string? Avatar { get; set; }

    [Column(TypeName = "varchar(100)")]
    public string? ProviderAccountId { get; set; }

    public DateTime? ExpriedRoleDate { get; set; }

    public bool IsBanned { get; set; }
    public bool IsDeleted { get; set; }

    public ERegion Region { get; set; }

    public long CommunicationScore { get; set; }
}
