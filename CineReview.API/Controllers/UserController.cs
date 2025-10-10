using CineReview.API.Attributes;
using CineReview.Domain.AggregatesModel.ReviewAggregates;
using CineReview.Domain.AggregatesModel.UserAggregates;
using CineReview.Domain.Enums;
using Common.Interfaces.Messaging;
using Common.Models;
using Common.SeedWork;
using Common.Shared.Models.Emails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Domain.Models.UserModels;

namespace CineReview.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : CommonController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISendMailPublisher _sendMailPublisher;

    public UserController(IUnitOfWork unitOfWork, ISendMailPublisher sendMailPublisher)
    {
        _unitOfWork = unitOfWork;
        _sendMailPublisher = sendMailPublisher;
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = GetUserIdByToken();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var profile = await GetUserProfileByIdAsync(userId.Value, cancellationToken);
        if (profile is null)
        {
            return NotFound(new ServiceResponse<UserProfileResponseModel>("User not found"));
        }

        return Ok(new ServiceResponse<UserProfileResponseModel>(profile));
    }

    [HttpGet("{userName}")]
    public async Task<IActionResult> GetProfileByUserName(string userName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return BadRequest(new ServiceResponse<UserProfileResponseModel>("Username is required"));
        }

        var normalized = userName.Trim();
        var profile = await GetUserProfileByUserNameAsync(normalized, cancellationToken);

        if (profile is null)
        {
            return NotFound(new ServiceResponse<UserProfileResponseModel>("User not found"));
        }

        return Ok(new ServiceResponse<UserProfileResponseModel>(profile));
    }

    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailMessage message)
    {
        await _sendMailPublisher.SendMailAsync(message);
        return Ok();
    }

    private async Task<UserProfileResponseModel?> GetUserProfileByIdAsync(int userId, CancellationToken cancellationToken)
    {
        return await BuildUserProfileAsync(
            _unitOfWork.Repository<User>().GetQueryable()
                .Where(user => user.Id == userId && !user.IsDeleted),
            cancellationToken);
    }

    private async Task<UserProfileResponseModel?> GetUserProfileByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        return await BuildUserProfileAsync(
            _unitOfWork.Repository<User>().GetQueryable()
                .Where(user => user.UserName == userName && !user.IsDeleted),
            cancellationToken);
    }

    private async Task<UserProfileResponseModel?> BuildUserProfileAsync(IQueryable<User> query, CancellationToken cancellationToken)
    {
        var projection = await query
            .AsNoTracking()
            .Select(user => new
            {
                user.Id,
                user.UserName,
                user.FullName,
                user.Email,
                user.Avatar,
                user.ExpriedRoleDate,
                user.CreatedOnUtc,
                user.CommunicationScore,
                user.Region,
                user.IsBanned,
                user.IsDeleted
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (projection is null)
        {
            return null;
        }

        var stats = await _unitOfWork.Repository<Review>().GetQueryable()
            .AsNoTracking()
            .Where(review => review.UserId == projection.Id
                && (review.Type == ReviewType.Tag || review.Status == ReviewStatus.Released))
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Fair = group.Count(review => review.CommunicationScore > 0),
                Unfair = group.Count(review => review.CommunicationScore < 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var response = new UserProfileResponseModel
        {
            Id = projection.Id,
            UserName = projection.UserName,
            FullName = projection.FullName,
            Email = projection.Email,
            Avatar = projection.Avatar,
            ExpriedRoleDate = projection.ExpriedRoleDate,
            CreatedOnUtc = projection.CreatedOnUtc,
            CommunicationScore = projection.CommunicationScore,
            Region = projection.Region,
            IsBanned = projection.IsBanned,
            IsDeleted = projection.IsDeleted
        };

        if (stats is not null)
        {
            response.ReviewStats.TotalReviews = stats.Total;
            response.ReviewStats.FairReviews = stats.Fair;
            response.ReviewStats.UnfairReviews = stats.Unfair;
        }

        return response;
    }
}
