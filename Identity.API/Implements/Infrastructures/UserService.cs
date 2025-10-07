using Common.Enums;
using Common.Helpers;
using Common.Models;
using Common.SeedWork;
using Common.Shared.Models.Users;
using Identity.Domain.AggregatesModel.RoleAggregates;
using Identity.Domain.AggregatesModel.UserAggregates;
using Identity.Domain.Interfaces.Messagings;
using Identity.Domain.Interfaces.Services;
using Identity.Domain.Models.Users;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Implements.Infrastructures;

public class UserService : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly ISyncUserPortalPublisher _syncUserPortalPublisher;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ISyncUserPortalPublisher syncUserPortalPublisher,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _syncUserPortalPublisher = syncUserPortalPublisher;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResponse<bool>> UpdateAsync(int id, UserUpdateRequestModel userModel)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return new ServiceResponse<bool>("error_user_not_found");
        }

        user.FullName = userModel.FullName;
        user.Region = userModel.Region;

        user.IsBanned = userModel.IsBanned;
        user.LockoutEnd = userModel.IsBanned ? DateTime.UtcNow : null;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return new ServiceResponse<bool>(result.Errors.Select(o => o.Description).JoinSeparator() ?? string.Empty);
        }

        // Update role
        if (userModel.Roles?.Count > 0)
        {
            var allRoles = _roleManager.Roles.ToList();
            var dbRoles = allRoles.ConvertAll(o => o.Name);

            var isValidRoles = userModel.Roles.TrueForAll(dbRoles.Contains);
            if (!isValidRoles)
            {
                return new ServiceResponse<bool>("error_roles_is_invalid");
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            var rolesToAdd = userModel.Roles.Except(userRoles);
            var rolesToRemove = userRoles.Except(userModel.Roles);

            await _userManager.AddToRolesAsync(user, rolesToAdd);
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }
        else
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, userRoles);
            }
        }

        // Sync user portal when update
        await _syncUserPortalPublisher.SyncUserPortalAsync(new SyncUserPortalMessage
        {
            UserId = user.Id,
            FullName = user.FullName,
            IsBanned = user.IsBanned,
            Region = user.Region
        });

        return new ServiceResponse<bool>(true);
    }

    public async Task<ServiceResponse<PagingCommonResponse<UserPagingModel>>> GetPagingAsync(UserPagingRequestModel request)
    {
        // Paging shortcut linq to get paging user activity
        var totalRecords = await _unitOfWork.QueryAsync<long>(
            @"
                SELECT
                    COUNT(1)
                FROM
                    [User] u
                WHERE
                    (
                        coalesce(@SearchTerm, '') = ''
                        OR (u.Email LIKE '' + @SearchTerm + '%')
                        OR (u.UserName LIKE '' + @SearchTerm + '%')
                    )
                    AND (@IsBanned IS NULL OR u.IsBanned = @IsBanned)
                    AND u.IsDeleted = 0
            ",
            new Dictionary<string, object?>
            {
                { "@SearchTerm", request.SearchTerm },
                { "@IsBanned", request.IsBanned },
                { "@IsDeleted", request.IsDeleted },
            },
            commandType: System.Data.CommandType.Text
        );

        var users = await _unitOfWork.QueryAsync<UserPagingModel>(
            @"
                SELECT
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.UserName,
                    u.Avatar,
                    u.Region,
                    GROUP_CONCAT(r.Name, ',') AS [Roles],
                    u.IsBanned,
                    u.IsDeleted,
                    u.CreatedOnUtc,
                    u.UpdatedOnUtc
                FROM [User] u
                    LEFT JOIN AspNetUserRoles ur on u.Id = ur.UserId
                    LEFT JOIN AspNetRoles r on ur.RoleId = r.Id
                WHERE
                    (
                        coalesce(@SearchTerm, '') = ''
                        OR (u.Email LIKE '' + @SearchTerm + '%')
                        OR (u.UserName LIKE '' + @SearchTerm + '%')
                    )
                    AND (@IsBanned IS NULL OR u.IsBanned = @IsBanned)
                    AND u.IsDeleted = 0
                GROUP BY
                    u.Id, u.FullName, u.Email, u.UserName, u.Avatar, u.Region, u.IsBanned, u.IsDeleted, u.CreatedOnUtc, u.UpdatedOnUtc
                ORDER BY
                    u.Id desc
                LIMIT
                    @PageSize
                OFFSET
                    (@PageNumber - 1) * @PageSize
            ",
            new Dictionary<string, object?>
            {
                { "@SearchTerm", request.SearchTerm },
                { "@IsBanned", request.IsBanned },
                { "@IsDeleted", request.IsDeleted },
                { "@PageNumber", request.PageNumber },
                { "@PageSize", request.PageSize }
            },
            commandType: System.Data.CommandType.Text
        );

        return new ServiceResponse<PagingCommonResponse<UserPagingModel>>(new PagingCommonResponse<UserPagingModel>
        {
            RowNum = totalRecords.FirstOrDefault(),
            Data = users
        });
    }

    public async Task<ServiceResponse<List<UserPagingModel>>> GetPartnersAsync()
    {
        var users = await _userManager.GetUsersInRoleAsync(CommonHelper.GetDescription(ERoles.Partner));
        return new ServiceResponse<List<UserPagingModel>>(users.Select(o => new UserPagingModel
        {
            Id = o.Id,
            FullName = o.FullName,
            Avatar = o.Avatar,
            Email = o.Email!
        }).ToList());
    }
}
