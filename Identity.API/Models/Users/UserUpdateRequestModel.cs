using System.ComponentModel.DataAnnotations;

namespace Identity.Domain.Models.Users;

public class UserUpdateRequestModel
{
    [Required(ErrorMessage = "USER_FULLNAME_REQUIRED")]
    [MaxLength(250, ErrorMessage = "USER_FULLNAME_MAX_LENGTH")]
    public string FullName { get; set; } = null!;

    public List<string>? Roles { get; set; }
    public bool IsBanned { get; set; }
    public string? Region { get; set; }
}