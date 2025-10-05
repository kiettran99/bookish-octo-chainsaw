namespace Portal.Domain.Models.UserModels;

public class UserProfileResponseModel
{
    public int Id { get; set; }
    public string UserName { get; set; } = null!;
    public string? FullName { get; set; }
    public string Email { get; set; } = null!;
    public string? Avatar { get; set; }
    public DateTime? ExpriedRoleDate { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}