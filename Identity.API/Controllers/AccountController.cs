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
    private readonly IConfiguration _configuration;

    public AccountController(
        IAccountService accountService,
        IJwtService jwtService,
        UserManager<User> userManager,
        IConfiguration configuration)
    {
        _accountService = accountService;
        _jwtService = jwtService;
        _userManager = userManager;
        _configuration = configuration;
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
    public IActionResult Login([FromQuery] string? redirectClientUrl = null)
    {
        // Đọc baseUrl từ cấu hình
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host.Value}";
        var properties = new AuthenticationProperties
        {
            RedirectUri = $"{baseUrl}/api/account/signin-google"
        };

        // Truyền redirectClientUrl vào state để Google trả lại trong callback
        if (!string.IsNullOrEmpty(redirectClientUrl))
        {
            properties.Items["redirect_client_url"] = redirectClientUrl;
        }

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

        // Lấy thông tin từ Google OAuth
        var name = result.Principal.FindFirstValue(ClaimTypes.Name);
        var givenName = result.Principal.FindFirstValue(ClaimTypes.GivenName);
        var surname = result.Principal.FindFirstValue(ClaimTypes.Surname);
        var picture = result.Principal.FindFirstValue("picture");
        var locale = result.Principal.FindFirstValue("locale");
        var emailVerified = result.Principal.FindFirstValue("email_verified");

        // Gọi service để xử lý đăng nhập/đăng ký
        var authResponse = await _accountService.GoogleAuthenticateAsync(new GoogleAuthenticateRequest
        {
            Email = email,
            Name = name,
            GivenName = givenName,
            FamilyName = surname,
            Picture = picture,
            Locale = locale,
            EmailVerified = bool.TryParse(emailVerified, out var verified) && verified
        });

        if (!authResponse.IsSuccess)
        {
            return BadRequest(authResponse.ErrorMessage);
        }

        var jwtToken = authResponse.Data?.JwtToken;

        // Lấy redirectClientUrl từ state (nếu client đã truyền lên)
        string? redirectClientUrl = null;
        result.Properties?.Items.TryGetValue("redirect_client_url", out redirectClientUrl);

        // Nếu không có redirectClientUrl từ client, dùng mặc định
        var clientUrl = !string.IsNullOrEmpty(redirectClientUrl)
            ? redirectClientUrl
            : "http://localhost:3001/validate";
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
