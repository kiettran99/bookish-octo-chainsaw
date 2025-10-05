namespace Identity.Domain.Models.Users;

public class UserPagingModel
{
    public int Id { get; set; }
    public string? FullName { get; set; }
    public string Email { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string? Avatar { get; set; }
    public string? Region { get; set; }

    public string? Roles { get; set; }
    public bool IsBanned { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}
