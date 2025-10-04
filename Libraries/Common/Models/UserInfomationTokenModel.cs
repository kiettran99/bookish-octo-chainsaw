namespace Common.Models;

public class UserInfomationTokenModel
{
    public int Id { get; set; }
    public string? FullName { get; set; }
    public string? ProviderAccountId { get; set; }
    public bool IsBanned { get; set; }

    public List<string>? Roles { get; set; }
}
