using Common.SeedWork;
using Microsoft.AspNetCore.Identity;

namespace Identity.Domain.AggregatesModel.RoleAggregates;

public class UserRole : IdentityUserRole<int>, IAggregateRoot
{
}
