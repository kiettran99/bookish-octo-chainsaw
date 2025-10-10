using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CineReview.Client.Features.Movies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CineReview.Client.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class MoviesController : ControllerBase
{
    private readonly IMovieDataProvider _movieDataProvider;
    private readonly ILogger<MoviesController> _logger;

    public MoviesController(IMovieDataProvider movieDataProvider, ILogger<MoviesController> logger)
    {
        _movieDataProvider = movieDataProvider ?? throw new ArgumentNullException(nameof(movieDataProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("summaries")]
    public async Task<IActionResult> GetSummaries([FromQuery] string? ids, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return Ok(new { items = Array.Empty<object>() });
        }

        var parsedIds = ids
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token, out var value) ? value : (int?)null)
            .Where(value => value.HasValue && value.Value > 0)
            .Select(value => value!.Value)
            .Distinct()
            .Take(25)
            .ToList();

        if (parsedIds.Count == 0)
        {
            return Ok(new { items = Array.Empty<object>() });
        }

        var summaries = new List<object>(parsedIds.Count);

        foreach (var movieId in parsedIds)
        {
            try
            {
                var detail = await _movieDataProvider.GetMovieDetailAsync(movieId, cancellationToken).ConfigureAwait(false);
                var summary = detail?.Summary;
                if (summary is null)
                {
                    continue;
                }

                summaries.Add(new
                {
                    id = summary.Id,
                    title = summary.Title,
                    posterUrl = summary.PosterUrl,
                    releaseDate = summary.ReleaseDate,
                    communityScore = summary.CommunityScore,
                    isNowPlaying = summary.IsNowPlaying
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải thông tin phim {MovieId}", movieId);
            }
        }

        return Ok(new { items = summaries });
    }
}
