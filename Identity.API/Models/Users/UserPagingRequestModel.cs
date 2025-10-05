using Common.Models;

namespace Identity.Domain.Models.Users;

public class UserPagingRequestModel : PagingCommonRequest
{
    public bool IsBanned { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
