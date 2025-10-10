using Microsoft.AspNetCore.Mvc;

namespace CineReview.Client.Controllers;

[Route("users")]
public sealed class UserController : Controller
{
    // Trang hồ sơ của chính mình hiển thị skeleton, dữ liệu được hydrate hoàn toàn ở client
    [HttpGet("/profile")]
    public IActionResult MyProfile([FromQuery] int page = 1)
    {
        var sanitizedPage = page < 1 ? 1 : page;
        ViewData["Title"] = "Hồ sơ của tôi";
        ViewData["InitialPage"] = sanitizedPage;
        return View("MyProfile");
    }

    [HttpGet("{userName}")]
    public IActionResult Profile(string userName, [FromQuery] int page = 1)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return RedirectToAction("Index", "Home");
        }

        var sanitizedPage = page < 1 ? 1 : page;
        var normalizedUserName = userName.Trim();

        ViewData["Title"] = $"Hồ sơ @{normalizedUserName}";
        ViewData["ProfileUserName"] = normalizedUserName;
        ViewData["InitialPage"] = sanitizedPage;

        return View("Profile");
    }
}
