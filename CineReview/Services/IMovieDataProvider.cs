using System.Threading;
using System.Threading.Tasks;
using CineReview.Models;

namespace CineReview.Services;

public interface IMovieDataProvider
{
    ValueTask<HomePageData> GetHomeAsync(CancellationToken cancellationToken = default);
    ValueTask<MovieProfile?> GetMovieDetailAsync(int id, CancellationToken cancellationToken = default);
    ValueTask<PaginatedMovies> GetNowPlayingAsync(int page, CancellationToken cancellationToken = default);
    ValueTask<PaginatedMovies> GetComingSoonAsync(int page, CancellationToken cancellationToken = default);
    ValueTask<MovieSearchResult> SearchMoviesAsync(MovieSearchRequest request, CancellationToken cancellationToken = default);
}
