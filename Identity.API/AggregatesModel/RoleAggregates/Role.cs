using Common.SeedWork;
using Microsoft.AspNetCore.Identity;

namespace Identity.Domain.AggregatesModel.RoleAggregates;

public class Role : IdentityRole<int>, IAggregateRoot
{
    public Role() : base() { }
    public Role(string roleName) : base(roleName)
    {
    }
}