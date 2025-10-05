using System.Diagnostics;
using CineReview.Client.Models;
using CineReview.Client.Features.Movies;
using Microsoft.AspNetCore.Mvc;

namespace CineReview.Client.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IMovieDataProvider _movieDataProvider;

    public HomeController(ILogger<HomeController> logger, IMovieDataProvider movieDataProvider)
    {
        _logger = logger;
        _movieDataProvider = movieDataProvider;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var data = await _movieDataProvider.GetHomeAsync(cancellationToken);
            return View(data);
        }
        catch (OperationCanceledException)
        {
            return View(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load home page data from movie provider");
            ViewData["LoadError"] = "Không thể tải dữ liệu phim. Vui lòng thử lại sau.";
            return View(null);
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
