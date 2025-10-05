using System.ComponentModel.DataAnnotations.Schema;
using Common.SeedWork;
using Microsoft.AspNetCore.Identity;

namespace Identity.Domain.AggregatesModel.UserAggregates;

public class User : IdentityUser<int>, IAggregateRoot
{
    [Column(TypeName = "nvarchar(250)")]
    public string? FullName { get; set; }

    [Column(TypeName = "varchar(200)")]
    public string? Avatar { get; set; }

    [Column(TypeName = "varchar(100)")]
    public string? ProviderAccountId { get; set; }

    public DateTime? ExpriedRoleDate { get; set; }

    public bool IsBanned { get; set; }
    public bool IsDeleted { get; set; }

    public string? Region { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}