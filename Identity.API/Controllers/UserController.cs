using Common.Enums;
using Identity.API.Attributes;
using Identity.Domain.Interfaces.Services;
using Identity.Domain.Models.Users;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(ERoles.Administrator)]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("paging")]
    public async Task<IActionResult> GetPagingAsync([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string searchTerm = "")
    {
        var response = await _userService.GetPagingAsync(new UserPagingRequestModel
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            SearchTerm = searchTerm
        });

        if (!response.IsSuccess)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateRequestModel userRequest)
    {
        var response = await _userService.UpdateAsync(id, userRequest);
        if (!response.IsSuccess)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("partners/all")]
    public async Task<IActionResult> GetPartnersAsync()
    {
        var response = await _userService.GetPartnersAsync();
        return Ok(response);
    }
}
