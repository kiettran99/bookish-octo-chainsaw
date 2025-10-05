using System.Security.Claims;
using Common.Enums;
using Common.Models;
using Identity.API.Attributes;
using Identity.Domain.AggregatesModel.UserAggregates;
using Identity.Domain.Interfaces.Infrastructures;
using Identity.Domain.Interfaces.Services;
using Identity.Domain.Models.Authenticates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IJwtService _jwtService;
    private readonly UserManager<User> _userManager;

    public AccountController(
        IAccountService accountService,
        IJwtService jwtService,
        UserManager<User> userManager)
    {
        _accountService = accountService;
        _jwtService = jwtService;
        _userManager = userManager;
    }

    [HttpPost("client-authenticate")]
    public async Task<IActionResult> ClientAuthenticateAsync([FromBody] ClientAuthenticateRequest request)
    {
        var response = await _accountService.ClientAuthenticateAsync(request);
        if (!response.IsSuccess)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("authenticate")]
    public IActionResult Login()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("Callback", "Account")
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("signin-google")]
    public async Task<IActionResult> Callback()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            return BadRequest("Google authentication failed.");
        }

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest("Please contact administrator to provide more information. Error Code: LN1");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return BadRequest("Please contact administrator to provide more information. Error Code: LN2");
        }

        // Check user should have Partner or Administrator role to can login
        var roles = await _userManager.GetRolesAsync(user);

        if (!roles.Contains(ERoles.Partner.ToString()) && !roles.Contains(ERoles.Administrator.ToString()))
        {
            return BadRequest("Please contact administrator to provide more information. Error Code: LN3");
        }

        // Token expries in 30 days
        var expirationInMinutes = 60 * 24 * 30;
        var jwtToken = _jwtService.GenerateJwtToken(user, expirationInMinutes);

        var clientUrl = "http://localhost:3001/validate";
        // var clientUrl = "https://kietdev1.github.io/itp-cms/validate";
        return Redirect($"{clientUrl}?token={jwtToken}");
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var userId = User.FindFirstValue("id");

        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("Please contact administrator to provide more information. Error Code: LN4");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return BadRequest("Please contact administrator to provide more information. Error Code: LN5");
        }

        // Response roles when user login
        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new ServiceResponse<AuthenticateResponse>(new AuthenticateResponse(user, string.Empty, roles.ToList())));
    }
}
