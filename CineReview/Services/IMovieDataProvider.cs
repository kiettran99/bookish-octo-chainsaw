using System.Threading;
using System.Threading.Tasks;
using CineReview.Models;

namespace CineReview.Services;

public interface IMovieDataProvider
{
    ValueTask<HomePageData> GetHomeAsync(CancellationToken cancellationToken = default);
    ValueTask<MovieProfile?> GetMovieDetailAsync(int id, CancellationToken cancellationToken = default);
}
