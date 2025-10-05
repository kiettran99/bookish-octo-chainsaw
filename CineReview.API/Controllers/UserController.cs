using CineReview.API.Attributes;
using CineReview.Domain.AggregatesModel.UserAggregates;
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
    public async Task<IActionResult> GetProfile()
    {
        var response = await _unitOfWork.Repository<User>().GetQueryable()
                            .Select(x => new UserProfileResponseModel
                            {
                                Id = x.Id,
                                FullName = x.FullName,
                                Email = x.Email,
                                UserName = x.UserName,
                                Avatar = x.Avatar,
                                ExpriedRoleDate = x.ExpriedRoleDate,
                                CreatedOnUtc = x.CreatedOnUtc
                            })
                            .FirstOrDefaultAsync();

        if (response == null)
        {
            return NotFound();
        }

        return Ok(new ServiceResponse<UserProfileResponseModel>(response));
    }

    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailMessage message)
    {
        await _sendMailPublisher.SendMailAsync(message);
        return Ok();
    }
}
