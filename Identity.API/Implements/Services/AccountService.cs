using Common.Models;
using Common.Shared.Models.Users;
using Common.ValueObjects;
using Identity.Domain.AggregatesModel.UserAggregates;
using Identity.Domain.Interfaces.Infrastructures;
using Identity.Domain.Interfaces.Messagings;
using Identity.Domain.Interfaces.Services;
using Identity.Domain.Models.Authenticates;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Implements.Services;

public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IJwtService _jwtService;
    private readonly ISyncUserPortalPublisher _syncUserPortalPublisher;

    public AccountService(
        ApplicationDbContext context,
        UserManager<User> userManager,
        IJwtService jwtService,
        ISyncUserPortalPublisher syncUserPortalPublisher)
    {
        _context = context;
        _userManager = userManager;
        _jwtService = jwtService;
        _syncUserPortalPublisher = syncUserPortalPublisher;
    }

    public async Task<ServiceResponse<AuthenticateResponse>> ClientAuthenticateAsync(ClientAuthenticateRequest model)
    {
        var user = await _context.Users.FirstOrDefaultAsync(o => o.ProviderAccountId == model.ProviderAccountId || o.Email == model.Email);
        if (user == null)
        {
            user = new User
            {
                UserName = model.Email.Split('@').FirstOrDefault(),
                Email = model.Email,
                FullName = model.Name ?? string.Empty,
                ProviderAccountId = model.ProviderAccountId,
                CreatedOnUtc = DateTime.UtcNow,
                Avatar = model.Image ?? string.Empty,
                EmailConfirmed = model.EmailVerified,
                Region = model.Region ?? "vi"
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                return new ServiceResponse<AuthenticateResponse>(string.Join(", ", result.Errors.Select(o => o.Description)));
            }

            // Add role User as default
            await _userManager.AddToRolesAsync(user, [CommonConst.RoleName.User]);

            // Sync portal create user event with message queue
            await _syncUserPortalPublisher.SyncUserPortalAsync(new SyncUserPortalMessage
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                UserName = user.UserName ?? string.Empty,
                Avatar = user.Avatar,
                ProviderAccountId = user.ProviderAccountId,
                Region = user.Region,
                IsNewUser = true
            });
        }
        else if (user.FullName != model.Name || user.Avatar != model.Image || user.Region != (model.Region ?? "vi"))
        {
            user.FullName = model.Name ?? string.Empty;
            user.Avatar = model.Image ?? string.Empty;
            user.Region = model.Region ?? "vi";
            user.UpdatedOnUtc = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Sync portal update user event with message queue
            await _syncUserPortalPublisher.SyncUserPortalAsync(new SyncUserPortalMessage
            {
                UserId = user.Id,
                FullName = user.FullName,
                Avatar = user.Avatar,
                Region = user.Region,
                IsUpdateAvatar = true
            });
        }

        // Token expries in 30 days
        var expirationInMinutes = 60 * 24 * 30;
        var jwtToken = _jwtService.GenerateJwtToken(user, expirationInMinutes);

        // Response roles when user login
        var roles = await _userManager.GetRolesAsync(user);

        return new ServiceResponse<AuthenticateResponse>(new AuthenticateResponse(user, jwtToken, roles.ToList()));
    }

    public async Task<ServiceResponse<AuthenticateResponse>> GoogleAuthenticateAsync(GoogleAuthenticateRequest model)
    {
        var user = await _context.Users.FirstOrDefaultAsync(o => o.Email == model.Email);
        if (user == null)
        {
            user = new User
            {
                UserName = model.Email.Split('@').FirstOrDefault(),
                Email = model.Email,
                FullName = model.Name ?? string.Empty,
                CreatedOnUtc = DateTime.UtcNow,
                Avatar = model.Picture ?? string.Empty,
                EmailConfirmed = model.EmailVerified,
                Region = model.Locale ?? "vi",
                ProviderAccountId = model.ProviderAccountId
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                return new ServiceResponse<AuthenticateResponse>(string.Join(", ", result.Errors.Select(o => o.Description)));
            }

            // Add role Partner as default for Google OAuth users
            await _userManager.AddToRolesAsync(user, [CommonConst.RoleName.User]);

            // Sync portal create user event with message queue
            await _syncUserPortalPublisher.SyncUserPortalAsync(new SyncUserPortalMessage
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                UserName = user.UserName ?? string.Empty,
                Avatar = user.Avatar,
                ProviderAccountId = user.ProviderAccountId,
                Region = user.Region,
                IsNewUser = true
            });
        }
        else
        {
            // Update user information if changed
            bool isUpdated = false;

            if (!string.IsNullOrEmpty(model.Name) && user.FullName != model.Name)
            {
                user.FullName = model.Name;
                isUpdated = true;
            }

            if (!string.IsNullOrEmpty(model.Picture) && user.Avatar != model.Picture)
            {
                user.Avatar = model.Picture;
                isUpdated = true;
            }

            if (!string.IsNullOrEmpty(model.Locale) && user.Region != model.Locale)
            {
                user.Region = model.Locale;
                isUpdated = true;
            }

            if (isUpdated)
            {
                user.UpdatedOnUtc = DateTime.UtcNow;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // Sync portal update user event with message queue
                await _syncUserPortalPublisher.SyncUserPortalAsync(new SyncUserPortalMessage
                {
                    UserId = user.Id,
                    FullName = user.FullName ?? string.Empty,
                    Avatar = user.Avatar ?? string.Empty,
                    Region = user.Region ?? "vi",
                    IsUpdateAvatar = true
                });
            }
        }

        // Check user should have Partner or Administrator role to can login
        var roles = await _userManager.GetRolesAsync(user);

        // Token expries in 30 days
        var expirationInMinutes = 60 * 24 * 30;
        var jwtToken = _jwtService.GenerateJwtToken(user, expirationInMinutes);

        return new ServiceResponse<AuthenticateResponse>(new AuthenticateResponse(user, jwtToken, roles.ToList()));
    }
}
