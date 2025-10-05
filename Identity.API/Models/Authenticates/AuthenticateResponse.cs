using Identity.Domain.AggregatesModel.UserAggregates;

namespace Identity.Domain.Models.Authenticates;

public class AuthenticateResponse
{
    public int? Id { get; set; }
    public string? FullName { get; set; }
    public string? UserName { get; set; }
    public string? Avatar { get; set; }
    public string? Email { get; set; }
    public string? JwtToken { get; set; }

    public List<string> Roles { get; set; }
    public DateTime? ExpriedRoleDate { get; set; }
    public DateTime? CreatedOnUtc { get; set; }

    public AuthenticateResponse(User user, string? jwtToken, List<string> roles)
    {
        Id = user.Id;
        FullName = user.FullName;
        UserName = user.UserName;
        Avatar = user.Avatar;
        Email = user.Email;
        JwtToken = jwtToken;

        Roles = roles;
        ExpriedRoleDate = user?.ExpriedRoleDate;
        CreatedOnUtc = user?.CreatedOnUtc;
    }
}
