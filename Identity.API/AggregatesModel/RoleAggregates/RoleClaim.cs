using Common.SeedWork;
using Microsoft.AspNetCore.Identity;

namespace Identity.Domain.AggregatesModel.RoleAggregates;

public class RoleClaim : IdentityRoleClaim<int>, IAggregateRoot
{
}
