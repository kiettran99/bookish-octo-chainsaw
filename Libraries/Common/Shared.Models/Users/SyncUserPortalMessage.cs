namespace Common.Shared.Models.Users;

public class SyncUserPortalMessage
{
    public int UserId { get; set; }

    #region New User Information
    public string? ProviderAccountId { get; set; }
    public string Email { get; set; } = null!;
    public string UserName { get; set; } = null!;
    #endregion

    public string FullName { get; set; } = null!;
    public string? Avatar { get; set; }
    public string? Region { get; set; }

    public bool IsNewUser { get; set; }
    public bool IsUpdateAvatar { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsBanned { get; set; }
}
