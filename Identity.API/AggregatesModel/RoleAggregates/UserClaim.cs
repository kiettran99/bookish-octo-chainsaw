using Common.SeedWork;
using Microsoft.AspNetCore.Identity;

namespace Identity.Domain.AggregatesModel.RoleAggregates;

public class UserClaim : IdentityUserClaim<int>, IAggregateRoot
{
}