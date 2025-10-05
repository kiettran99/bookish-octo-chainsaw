using CineReview.Client.Features.Movies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CineReview.Client.Controllers;

[Route("movies")]
public sealed class MoviesController : Controller
{
    private readonly IMovieDataProvider _movieDataProvider;
    private readonly ILogger<MoviesController> _logger;

    public MoviesController(IMovieDataProvider movieDataProvider, ILogger<MoviesController> logger)
    {
        _movieDataProvider = movieDataProvider;
        _logger = logger;
    }

    [HttpGet("now-playing")]
    public async Task<IActionResult> NowPlaying([FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var sanitizedPage = page < 1 ? 1 : page;

        try
        {
            var result = await _movieDataProvider.GetNowPlayingAsync(sanitizedPage, cancellationToken);
            if (result.TotalResults > 0 && sanitizedPage > result.TotalPages && result.TotalPages > 0)
            {
                return RedirectToActionPermanent(nameof(NowPlaying), new { page = result.TotalPages });
            }

            return View(result);
        }
        catch (OperationCanceledException)
        {
            return View(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load now playing movies for page {Page}", page);
            ViewData["LoadError"] = "Không thể tải danh sách phim đang chiếu. Vui lòng thử lại sau.";
            return View(null);
        }
    }

    [HttpGet("coming-soon")]
    public async Task<IActionResult> ComingSoon([FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var sanitizedPage = page < 1 ? 1 : page;

        try
        {
            var result = await _movieDataProvider.GetComingSoonAsync(sanitizedPage, cancellationToken);
            if (result.TotalResults > 0 && sanitizedPage > result.TotalPages && result.TotalPages > 0)
            {
                return RedirectToActionPermanent(nameof(ComingSoon), new { page = result.TotalPages });
            }

            return View(result);
        }
        catch (OperationCanceledException)
        {
            return View(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load coming soon movies for page {Page}", page);
            ViewData["LoadError"] = "Không thể tải danh sách phim sắp chiếu. Vui lòng thử lại sau.";
            return View(null);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = await _movieDataProvider.GetMovieDetailAsync(id, cancellationToken);
            if (profile is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
            }

            return View(profile);
        }
        catch (OperationCanceledException)
        {
            return View((MovieProfile?)null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load movie detail for id {MovieId}", id);
            ViewData["LoadError"] = "Không thể tải chi tiết phim. Vui lòng thử lại sau.";
            return View((MovieProfile?)null);
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "query")] string? query,
        [FromQuery(Name = "genre")] string? genre,
        [FromQuery(Name = "from")] DateTime? releaseFrom,
        [FromQuery(Name = "to")] DateTime? releaseTo,
        [FromQuery(Name = "minScore")] double? minScore,
        [FromQuery(Name = "region")] string? region,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var sanitizedPage = page < 1 ? 1 : page;
        var criteria = new MovieSearchRequest(query, sanitizedPage, genre, releaseFrom, releaseTo, minScore, region);

        try
        {
            var result = await _movieDataProvider.SearchMoviesAsync(criteria, cancellationToken);
            if (result.Page.TotalResults > 0 && sanitizedPage > result.Page.TotalPages && result.Page.TotalPages > 0)
            {
                return RedirectToActionPermanent(nameof(Search), new
                {
                    query,
                    genre,
                    from = releaseFrom?.ToString("yyyy-MM-dd"),
                    to = releaseTo?.ToString("yyyy-MM-dd"),
                    minScore,
                    region,
                    page = result.Page.TotalPages
                });
            }

            var viewModel = new MovieSearchViewModel
            {
                Criteria = criteria,
                Result = result
            };

            return View(viewModel);
        }
        catch (OperationCanceledException)
        {
            var canceledModel = new MovieSearchViewModel
            {
                Criteria = criteria
            };

            return View(canceledModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform movie search with query {Query}", query);
            var errorModel = new MovieSearchViewModel
            {
                Criteria = criteria,
                ErrorMessage = "Không thể tìm kiếm phim vào lúc này. Vui lòng thử lại sau."
            };

            return View(errorModel);
        }
    }
}
