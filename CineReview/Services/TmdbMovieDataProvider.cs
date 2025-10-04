using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CineReview.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CineReview.Services;

public sealed class TmdbMovieDataProvider : IMovieDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly TmdbOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TmdbMovieDataProvider> _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    private const string GenreCacheKey = "tmdb:genres:v1";
    private const string CountryCacheKey = "tmdb:countries:v1";

    public TmdbMovieDataProvider(
        HttpClient httpClient,
        IOptions<TmdbOptions> options,
        IMemoryCache cache,
        ILogger<TmdbMovieDataProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_options.ApiKey) && string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new InvalidOperationException("TMDB API credentials are missing. Configure 'Tmdb:ApiKey' or 'Tmdb:AccessToken'.");
        }

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl, UriKind.Absolute);
        }

        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        }

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async ValueTask<HomePageData> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        var genreLookup = await GetGenreLookupAsync(cancellationToken).ConfigureAwait(false);

        var nowPlayingTask = GetMovieSummariesAsync("movie/now_playing", isNowPlaying: true, genreLookup, 8, cancellationToken);
        var comingSoonTask = GetMovieSummariesAsync("movie/upcoming", isNowPlaying: false, genreLookup, 8, cancellationToken);
        var trendingTask = GetMovieSummariesAsync("trending/movie/week", isNowPlaying: null, genreLookup, 8, cancellationToken);

        await Task.WhenAll(nowPlayingTask, comingSoonTask, trendingTask).ConfigureAwait(false);

        var trending = trendingTask.Result;
        var nowPlaying = nowPlayingTask.Result;
        var comingSoon = comingSoonTask.Result;

        var featuredCandidate = trending.FirstOrDefault()
            ?? nowPlaying.FirstOrDefault()
            ?? comingSoon.FirstOrDefault();

        MovieSummary featured;
        if (featuredCandidate is not null)
        {
            var detailedSummary = await GetMovieSummaryWithDetailAsync(featuredCandidate.Id, genreLookup, cancellationToken).ConfigureAwait(false);
            featured = detailedSummary ?? featuredCandidate;
        }
        else
        {
            throw new InvalidOperationException("Không thể lấy dữ liệu phim nổi bật từ TMDB.");
        }

        var latestReviews = BuildSampleReviews(featured);
        var editorials = BuildEditorialSpotlights();

        return new HomePageData(
            Featured: featured,
            NowPlaying: nowPlaying,
            ComingSoon: comingSoon,
            TrendingThisWeek: trending,
            LatestReviews: latestReviews,
            EditorialSpots: editorials);
    }

    public async ValueTask<MovieProfile?> GetMovieDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var genreLookup = await GetGenreLookupAsync(cancellationToken).ConfigureAwait(false);
        var detail = await GetMovieDetailPayloadAsync(id, includeExtended: true, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            return null;
        }

        var summary = MapToSummary(detail, genreLookup, inferIsNowPlaying: null);
        var certification = ExtractCertification(detail) ?? "Đang cập nhật";
        var highlights = BuildHighlights(detail, summary, certification);
        var watchOptions = BuildWatchOptions(detail, summary);
        var ticketPolicy = BuildTicketPolicy(summary);
        var cast = BuildTopCast(detail);
        var videos = BuildVideos(detail);
        var reviews = BuildDetailReviews(summary);
        var recommendations = BuildRecommendations(detail, genreLookup);

        var runtime = detail.Runtime ?? 0;
        var status = string.IsNullOrWhiteSpace(detail.Status) ? "Đang cập nhật" : detail.Status;

        return new MovieProfile(
            Summary: summary,
            RuntimeMinutes: runtime,
            Status: status,
            Certification: certification,
            TicketPolicyNote: ticketPolicy,
            Highlights: highlights,
            WatchOptions: watchOptions,
            TopCast: cast,
            Videos: videos,
            Reviews: reviews,
            Recommended: recommendations);
    }

    public ValueTask<PaginatedMovies> GetNowPlayingAsync(int page, CancellationToken cancellationToken = default)
        => GetPagedMoviesAsync(
            "movie/now_playing",
            page,
            isNowPlaying: true,
            title: "Đang chiếu nổi bật",
            description: "Các suất chiếu đang mở bán, hãy xác thực vé để bật review.",
            cancellationToken);

    public ValueTask<PaginatedMovies> GetComingSoonAsync(int page, CancellationToken cancellationToken = default)
        => GetPagedMoviesAsync(
            "movie/upcoming",
            page,
            isNowPlaying: false,
            title: "Sắp công chiếu",
            description: "Đặt lịch phát sóng để không bỏ lỡ vé early-access.",
            cancellationToken);

    public async ValueTask<MovieSearchResult> SearchMoviesAsync(MovieSearchRequest request, CancellationToken cancellationToken = default)
    {
        var safePage = request.Page < 1 ? 1 : request.Page;
        var genreLookup = await GetGenreLookupAsync(cancellationToken).ConfigureAwait(false);
        var countries = await GetCountryOptionsAsync(cancellationToken).ConfigureAwait(false);

        var hasQuery = !string.IsNullOrWhiteSpace(request.Query);
        var endpoint = hasQuery ? "search/movie" : "discover/movie";
        var query = new Dictionary<string, string?>
        {
            ["page"] = safePage.ToString(CultureInfo.InvariantCulture),
            ["include_adult"] = "false"
        };

        if (hasQuery)
        {
            query["query"] = request.Query;
        }

        if (!string.IsNullOrWhiteSpace(request.Region))
        {
            query["region"] = request.Region;
        }

        if (!string.IsNullOrWhiteSpace(request.Genre) && !hasQuery)
        {
            query["with_genres"] = request.Genre;
        }

        if (request.ReleaseFrom.HasValue && !hasQuery)
        {
            query["primary_release_date.gte"] = request.ReleaseFrom.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (request.ReleaseTo.HasValue && !hasQuery)
        {
            query["primary_release_date.lte"] = request.ReleaseTo.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (request.MinScore.HasValue && !hasQuery)
        {
            query["vote_average.gte"] = request.MinScore.Value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        if (!hasQuery)
        {
            query["sort_by"] = "popularity.desc";
        }

        var payload = await GetFromTmdbAsync<TmdbMovieListResponse>(endpoint, query, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            var emptyPage = new PaginatedMovies(Array.Empty<MovieSummary>(), safePage, 1, 0, "Tìm kiếm phim", "Không lấy được dữ liệu từ TMDB. Vui lòng thử lại sau.");
            return new MovieSearchResult(emptyPage, BuildGenreOptions(genreLookup), countries);
        }

        var summaries = new List<MovieSummary>();
        if (payload.Results is not null)
        {
            foreach (var item in payload.Results)
            {
                if (item is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.Genre) && hasQuery)
                {
                    if (!int.TryParse(request.Genre, NumberStyles.Integer, CultureInfo.InvariantCulture, out var genreId) ||
                        item.GenreIds is null || !item.GenreIds.Contains(genreId))
                    {
                        continue;
                    }
                }

                var releaseDate = ParseDate(item.ReleaseDate);

                if (request.ReleaseFrom.HasValue && hasQuery)
                {
                    if (releaseDate is null || releaseDate.Value.Date < request.ReleaseFrom.Value.Date)
                    {
                        continue;
                    }
                }

                if (request.ReleaseTo.HasValue && hasQuery)
                {
                    if (releaseDate is null || releaseDate.Value.Date > request.ReleaseTo.Value.Date)
                    {
                        continue;
                    }
                }

                if (request.MinScore.HasValue && hasQuery)
                {
                    if (item.VoteAverage < request.MinScore.Value)
                    {
                        continue;
                    }
                }

                var summary = MapToSummary(item, genreLookup, isNowPlaying: null);
                summaries.Add(summary);
            }
        }

        var resolvedPage = payload.Page <= 0 ? safePage : payload.Page;
        var resolvedTotalPages = payload.TotalPages <= 0 ? Math.Max(resolvedPage, 1) : payload.TotalPages;
        var resolvedTotalResults = payload.TotalResults;

        if (hasQuery)
        {
            resolvedTotalResults = Math.Min(resolvedTotalResults, resolvedTotalPages * summaries.Count);
        }

        var pageTitle = hasQuery ? $"Kết quả cho \"{request.Query}\"" : "Tìm kiếm phim";
        var description = resolvedTotalResults switch
        {
            0 => "Không tìm thấy phim khớp với bộ lọc hiện tại. Thử điều chỉnh tiêu chí và tìm lại.",
            1 => "Đã tìm thấy 1 phim khớp với bộ lọc của bạn.",
            _ => $"Đã tìm thấy {resolvedTotalResults:N0} phim phù hợp với bộ lọc."
        };

        var page = new PaginatedMovies(summaries, resolvedPage, resolvedTotalPages, resolvedTotalResults, pageTitle, description);
    return new MovieSearchResult(page, BuildGenreOptions(genreLookup), countries);
    }

    private async Task<IReadOnlyList<MovieSummary>> GetMovieSummariesAsync(
        string path,
        bool? isNowPlaying,
        IDictionary<int, string> genreLookup,
        int take,
        CancellationToken cancellationToken)
    {
        var payload = await GetFromTmdbAsync<TmdbMovieListResponse>(path, null, cancellationToken).ConfigureAwait(false);
        if (payload?.Results is null || payload.Results.Count == 0)
        {
            return Array.Empty<MovieSummary>();
        }

        var selected = payload.Results
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .OrderByDescending(item => item.Popularity)
            .Take(take)
            .ToArray();

        var summaries = new List<MovieSummary>(selected.Length);
        foreach (var item in selected)
        {
            var summary = await GetMovieSummaryWithDetailAsync(item.Id, genreLookup, cancellationToken).ConfigureAwait(false);
            if (summary is null)
            {
                summary = MapToSummary(item, genreLookup, isNowPlaying);
            }
            else if (isNowPlaying.HasValue)
            {
                summary = summary with
                {
                    IsNowPlaying = isNowPlaying.Value,
                    RequiresTicketVerification = DetermineTicketVerification(summary.ReleaseDate, summary.IsNowPlaying)
                };
            }

            if (summary is not null)
            {
                summaries.Add(summary);
            }
        }

        return summaries;
    }

    private async Task<MovieSummary?> GetMovieSummaryWithDetailAsync(int id, IDictionary<int, string> genreLookup, CancellationToken cancellationToken)
    {
        var detail = await GetMovieDetailPayloadAsync(id, includeExtended: false, cancellationToken).ConfigureAwait(false);
        return detail is null ? null : MapToSummary(detail, genreLookup, inferIsNowPlaying: null);
    }

    private async Task<IReadOnlyList<MovieFilterOption>> GetCountryOptionsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<IReadOnlyList<MovieFilterOption>>(CountryCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var payload = await GetFromTmdbAsync<List<TmdbCountry>>("configuration/countries", null, cancellationToken).ConfigureAwait(false);
        if (payload is null || payload.Count == 0)
        {
            return Array.Empty<MovieFilterOption>();
        }

        var options = payload
            .Where(country => !string.IsNullOrWhiteSpace(country.Iso3166) && !string.IsNullOrWhiteSpace(country.EnglishName))
            .OrderBy(country => country.EnglishName)
            .Select(country => new MovieFilterOption(country.Iso3166!, country.EnglishName!))
            .ToArray();

        _cache.Set(CountryCacheKey, options, _options.CacheDuration);
        return options;
    }

    private static IReadOnlyList<MovieFilterOption> BuildGenreOptions(IDictionary<int, string> genreLookup)
    {
        return genreLookup
            .Where(kv => kv.Key > 0 && !string.IsNullOrWhiteSpace(kv.Value))
            .OrderBy(kv => kv.Value)
            .Select(kv => new MovieFilterOption(kv.Key.ToString(CultureInfo.InvariantCulture), kv.Value))
            .ToArray();
    }

    private async ValueTask<PaginatedMovies> GetPagedMoviesAsync(
        string path,
        int requestedPage,
        bool? isNowPlaying,
        string title,
        string description,
        CancellationToken cancellationToken)
    {
        var safePage = requestedPage < 1 ? 1 : requestedPage;
        var query = new Dictionary<string, string?>
        {
            ["page"] = safePage.ToString(CultureInfo.InvariantCulture)
        };

        var genreLookup = await GetGenreLookupAsync(cancellationToken).ConfigureAwait(false);
        var payload = await GetFromTmdbAsync<TmdbMovieListResponse>(path, query, cancellationToken).ConfigureAwait(false);

        if (payload?.Results is null || payload.Results.Count == 0)
        {
            var totalPagesFallback = payload is null || payload.TotalPages <= 0 ? 1 : payload.TotalPages;
            return new PaginatedMovies(Array.Empty<MovieSummary>(), safePage, totalPagesFallback, payload?.TotalResults ?? 0, title, description);
        }

        var summaries = new List<MovieSummary>(payload.Results.Count);
        foreach (var item in payload.Results)
        {
            if (string.IsNullOrWhiteSpace(item.Title) && string.IsNullOrWhiteSpace(item.OriginalTitle))
            {
                continue;
            }

            summaries.Add(MapToSummary(item, genreLookup, isNowPlaying));
        }

        var resolvedPage = payload.Page <= 0 ? safePage : payload.Page;
        var resolvedTotalPages = payload.TotalPages <= 0 ? resolvedPage : payload.TotalPages;

        return new PaginatedMovies(
            summaries,
            resolvedPage,
            resolvedTotalPages,
            payload.TotalResults,
            title,
            description);
    }

    private MovieSummary MapToSummary(TmdbMovieDetail detail, IDictionary<int, string> genreLookup, bool? inferIsNowPlaying)
    {
        var releaseDate = ParseDate(detail.ReleaseDate) ?? DateTime.UtcNow;
        var genres = detail.Genres?.Select(g => g.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray()
                     ?? detail.GenreIds?.Select(id => genreLookup.TryGetValue(id, out var name) ? name : null)
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Select(name => name!)
                         .ToArray()
                     ?? Array.Empty<string>();

        var isNowPlaying = inferIsNowPlaying ?? DetermineNowPlayingStatus(releaseDate, detail.Status);
        var requiresTicket = DetermineTicketVerification(releaseDate, isNowPlaying);

        return new MovieSummary(
            Id: detail.Id,
            Title: detail.Title ?? detail.OriginalTitle ?? "Tên phim chưa cập nhật",
            Tagline: string.IsNullOrWhiteSpace(detail.Tagline) ? (detail.Overview ?? string.Empty) : detail.Tagline,
            PosterUrl: ResolveImageUrl(detail.PosterPath, _options.PosterSize),
            BackdropUrl: ResolveImageUrl(detail.BackdropPath, _options.BackdropSize),
            ReleaseDate: releaseDate,
            CommunityScore: detail.VoteAverage,
            Genres: genres,
            Overview: detail.Overview ?? string.Empty,
            IsNowPlaying: isNowPlaying,
            RequiresTicketVerification: requiresTicket
        );
    }

    private MovieSummary MapToSummary(TmdbMovieListItem item, IDictionary<int, string> genreLookup, bool? isNowPlaying)
    {
        var releaseDate = ParseDate(item.ReleaseDate) ?? DateTime.UtcNow;
        var genres = item.GenreIds?.Select(id => genreLookup.TryGetValue(id, out var name) ? name : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray() ?? Array.Empty<string>();

    var inferedNowPlaying = isNowPlaying ?? DetermineNowPlayingStatus(releaseDate, null);
        var requiresTicket = DetermineTicketVerification(releaseDate, inferedNowPlaying);

        return new MovieSummary(
            Id: item.Id,
            Title: item.Title ?? item.OriginalTitle ?? "Tên phim chưa cập nhật",
            Tagline: item.Overview ?? string.Empty,
            PosterUrl: ResolveImageUrl(item.PosterPath, _options.PosterSize),
            BackdropUrl: ResolveImageUrl(item.BackdropPath, _options.BackdropSize),
            ReleaseDate: releaseDate,
            CommunityScore: item.VoteAverage,
            Genres: genres,
            Overview: item.Overview ?? string.Empty,
            IsNowPlaying: inferedNowPlaying,
            RequiresTicketVerification: requiresTicket
        );
    }

    private async Task<IDictionary<int, string>> GetGenreLookupAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<IDictionary<int, string>>(GenreCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var payload = await GetFromTmdbAsync<TmdbGenreResponse>("genre/movie/list", null, cancellationToken).ConfigureAwait(false);
        var genres = payload?.Genres?.Where(g => g.Id > 0 && !string.IsNullOrWhiteSpace(g.Name))
            .ToDictionary(g => g.Id, g => g.Name) ?? new Dictionary<int, string>();

        _cache.Set(GenreCacheKey, genres, _options.CacheDuration);
        return genres;
    }

    private async Task<TmdbMovieDetail?> GetMovieDetailPayloadAsync(int id, bool includeExtended, CancellationToken cancellationToken)
    {
        var cacheKey = includeExtended ? $"tmdb:movie:{id}:full" : $"tmdb:movie:{id}:core";
        if (_cache.TryGetValue(cacheKey, out object? cachedObj) && cachedObj is TmdbMovieDetail cachedDetail)
        {
            return cachedDetail;
        }

        var query = includeExtended
            ? new Dictionary<string, string?>
            {
                ["append_to_response"] = "credits,videos,recommendations,release_dates"
            }
            : null;

        var detail = await GetFromTmdbAsync<TmdbMovieDetail>($"movie/{id}", query, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            return null;
        }

    TmdbMovieDetail materializedDetail = detail;
    _cache.Set(cacheKey, materializedDetail, _options.CacheDuration);
    return materializedDetail;
    }

    private async Task<T?> GetFromTmdbAsync<T>(string path, IDictionary<string, string?>? query, CancellationToken cancellationToken)
    {
        var uri = BuildRequestUri(path, query);
        try
        {
            var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TMDB request to {Path} failed with status {StatusCode}", path, (int)response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(_serializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TMDB request to {Path} failed", path);
            return default;
        }
    }

    private Uri BuildRequestUri(string path, IDictionary<string, string?>? query)
    {
        var builder = new UriBuilder(new Uri(_httpClient.BaseAddress!, path));

        var queryParameters = new List<string>();
        if (query is not null)
        {
            foreach (var kv in query)
            {
                if (!string.IsNullOrWhiteSpace(kv.Value))
                {
                    queryParameters.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiKey) && string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            queryParameters.Add($"api_key={Uri.EscapeDataString(_options.ApiKey!)}");
        }

        if (!string.IsNullOrWhiteSpace(_options.DefaultLanguage))
        {
            queryParameters.Add($"language={Uri.EscapeDataString(_options.DefaultLanguage)}");
        }

        if (!string.IsNullOrWhiteSpace(_options.DefaultRegion))
        {
            queryParameters.Add($"region={Uri.EscapeDataString(_options.DefaultRegion)}");
        }

        builder.Query = string.Join('&', queryParameters);
        return builder.Uri;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result))
        {
            return DateTime.SpecifyKind(result.Date, DateTimeKind.Utc);
        }

        return null;
    }

    private static bool DetermineNowPlayingStatus(DateTime releaseDateUtc, string? status)
    {
        if (string.Equals(status, "Released", StringComparison.OrdinalIgnoreCase))
        {
            return (DateTime.UtcNow - releaseDateUtc).TotalDays <= 90;
        }

        if (releaseDateUtc <= DateTime.UtcNow && (DateTime.UtcNow - releaseDateUtc).TotalDays <= 45)
        {
            return true;
        }

        return false;
    }

    private static bool DetermineTicketVerification(DateTime releaseDateUtc, bool isNowPlaying)
    {
        if (releaseDateUtc > DateTime.UtcNow)
        {
            return true;
        }

        return isNowPlaying;
    }

    private static string BuildTicketPolicy(MovieSummary summary)
    {
        if (summary.ReleaseDate > DateTime.UtcNow)
        {
            return $"Phim dự kiến khởi chiếu ngày {summary.ReleaseDate:dd/MM/yyyy} - mở đăng ký nhắc lịch và xác thực vé sau suất chiếu.";
        }

        if (summary.IsNowPlaying)
        {
            return "Phim đang chiếu - yêu cầu xác thực vé hợp lệ trước khi đăng review.";
        }

        return "Phim đã phát hành - bạn có thể viết review ngay mà không cần xác thực vé.";
    }

    private static IReadOnlyList<string> BuildHighlights(TmdbMovieDetail detail, MovieSummary summary, string certification)
    {
        var highlights = new List<string>
        {
            $"Điểm TMDB: {summary.CommunityScore:0.0}/10 từ {detail.VoteCount:N0} lượt bình chọn.",
            $"Thể loại nổi bật: {string.Join(", ", summary.Genres.Take(3))}."
        };

        if (detail.Runtime.HasValue && detail.Runtime.Value > 0)
        {
            highlights.Add($"Thời lượng dự kiến: {detail.Runtime.Value} phút.");
        }

        if (!string.IsNullOrWhiteSpace(certification) && !string.Equals(certification, "Đang cập nhật", StringComparison.OrdinalIgnoreCase))
        {
            highlights.Add($"Xếp hạng phát hành: {certification}.");
        }

        if (!string.IsNullOrWhiteSpace(detail.Tagline))
        {
            highlights.Add($"Tagline: \"{detail.Tagline}\"");
        }

        return highlights;
    }

    private static IReadOnlyList<string> BuildWatchOptions(TmdbMovieDetail detail, MovieSummary summary)
    {
        if (summary.ReleaseDate > DateTime.UtcNow)
        {
            return new[]
            {
                $"Khởi chiếu dự kiến {summary.ReleaseDate:dd/MM/yyyy}",
                "Mở đăng ký nhắc lịch trên CineReview",
                "Suất trải nghiệm sớm dành cho thành viên thân thiết"
            };
        }

        if (summary.IsNowPlaying)
        {
            return new[]
            {
                "Đặt vé trực tuyến tại các cụm rạp đối tác",
                "Suất chiếu phụ đề và lồng tiếng (nếu có)",
                "Xác thực vé điện tử ngay trong ứng dụng"
            };
        }

        return new[]
        {
            "Phát hành digital trên các nền tảng quốc tế",
            "Chờ cập nhật dịch vụ streaming tại Việt Nam",
            "Mua đĩa Blu-ray/4K từ nhà phát hành"
        };
    }

    private IReadOnlyList<CastMember> BuildTopCast(TmdbMovieDetail detail)
    {
        var cast = detail.Credits?.Cast;
        if (cast is null || cast.Count == 0)
        {
            return Array.Empty<CastMember>();
        }

        return cast
            .OrderBy(c => c.Order)
            .ThenByDescending(c => c.Popularity)
            .Take(8)
            .Select(c => new CastMember(
                Name: c.Name ?? "Đang cập nhật",
                Character: string.IsNullOrWhiteSpace(c.Character) ? "Vai diễn chưa cập nhật" : c.Character,
                HeadshotUrl: ResolveImageUrl(c.ProfilePath, "w185")))
            .ToArray();
    }

    private static IReadOnlyList<TrailerVideo> BuildVideos(TmdbMovieDetail detail)
    {
        var videos = detail.Videos?.Results;
        if (videos is null || videos.Count == 0)
        {
            return Array.Empty<TrailerVideo>();
        }

        return videos
            .Where(video => string.Equals(video.Site, "YouTube", StringComparison.OrdinalIgnoreCase))
            .Where(video => !string.IsNullOrWhiteSpace(video.Key))
            .OrderByDescending(video => video.Type == "Trailer")
            .ThenByDescending(video => video.PublishedAt)
            .Take(6)
            .Select(video => new TrailerVideo(
                Id: video.Id ?? video.Key!,
                Title: video.Name ?? "Video chính thức",
                ThumbnailUrl: $"https://img.youtube.com/vi/{video.Key}/hqdefault.jpg",
                VideoUrl: $"https://www.youtube.com/watch?v={video.Key}",
                Provider: "YouTube"))
            .ToArray();
    }

    private static string BuildUsername(string author)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return "@reviewer";
        }

        var normalized = new string(author
            .Trim()
            .ToLowerInvariant()
            .Select(ch =>
                char.IsLetterOrDigit(ch) ? ch :
                ch is ' ' or '.' or '-' ? '_' :
                '\0')
            .Where(ch => ch != '\0')
            .ToArray());

        normalized = normalized.Trim('_');
        if (string.IsNullOrEmpty(normalized))
        {
            normalized = "cinefan";
        }

        return '@' + normalized;
    }

    private static IReadOnlyList<ReviewSnapshot> BuildDetailReviews(MovieSummary summary)
    {
        var releaseLabel = summary.ReleaseDate > DateTime.UtcNow
            ? "Suất chiếu sớm"
            : "Khán giả CineReview";

        static string BuildBadge()
        {
            var badges = new[]
            {
                "Vua xem phim",
                "Người review có tiếng",
                "Fan cứng CineReview",
                "Ít tiền nhưng ham xem phim",
                "Thành viên cộng đồng",
                "Chuyên gia review",
                "Người truyền cảm hứng",
                "Reviewer kỳ cựu",
                "Người săn suất đầu",
                "Fan suất đầu"
            };
            var rnd = new Random();
            return badges[rnd.Next(badges.Length)];
        }

        return new[]
        {
            new ReviewSnapshot(
                Id: $"rv-{summary.Id}-preview",
                Author: "Lan Phạm",
                Username: BuildUsername("Lan Phạm"),
                AvatarUrl: "https://i.pravatar.cc/96?img=36",
                BadgeLabel: BuildBadge(),
                Excerpt: "Cốt truyện được xử lý mạch lạc, hình ảnh rất ấn tượng so với kỳ vọng ban đầu.",
                Rating: 8,
                FairVotes: summary.RequiresTicketVerification ? 122 : 96,
                UnfairVotes: 5,
                SupportScore: (summary.RequiresTicketVerification ? 122 : 96) - 5,
                CreatedAt: DateTime.UtcNow.AddDays(-2),
                IsTicketVerified: summary.RequiresTicketVerification,
                ContextLabel: summary.Title,
                Location: "HCMC, VN"),
            new ReviewSnapshot(
                Id: $"rv-{summary.Id}-buzz",
                Author: "Minh Đức",
                Username: BuildUsername("Minh Đức"),
                AvatarUrl: "https://i.pravatar.cc/96?img=14",
                BadgeLabel: BuildBadge(),
                Excerpt: "Đáng để đặt vé suất đầu tiên, đặc biệt nếu bạn yêu thích thể loại này.",
                Rating: 9,
                FairVotes: summary.RequiresTicketVerification ? 101 : 82,
                UnfairVotes: 9,
                SupportScore: (summary.RequiresTicketVerification ? 101 : 82) - 9,
                CreatedAt: DateTime.UtcNow.AddDays(-5),
                IsTicketVerified: false,
                ContextLabel: releaseLabel,
                Location: "Ha Noi, VN")
        };
    }

    private IReadOnlyList<MovieSummary> BuildRecommendations(TmdbMovieDetail detail, IDictionary<int, string> genreLookup)
    {
        var recommendations = detail.Recommendations?.Results;
        if (recommendations is null || recommendations.Count == 0)
        {
            return Array.Empty<MovieSummary>();
        }

        return recommendations
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .OrderByDescending(item => item.Popularity)
            .Take(6)
            .Select(item => MapToSummary(item, genreLookup, isNowPlaying: null))
            .ToArray();
    }

    private static ReviewSnapshot[] BuildSampleReviews(MovieSummary featured)
    {
        return new[]
        {
            new ReviewSnapshot(
                Id: "home-rv-1",
                Author: "Gia Hân",
                Username: BuildUsername("Gia Hân"),
                AvatarUrl: "https://i.pravatar.cc/96?img=52",
                BadgeLabel: featured.RequiresTicketVerification ? "Fan suất đầu" : "Thành viên cộng đồng",
                Excerpt: $"{featured.Title} mang đến trải nghiệm điện ảnh rất đáng chờ đợi.",
                Rating: 9,
                FairVotes: featured.RequiresTicketVerification ? 148 : 112,
                UnfairVotes: 7,
                SupportScore: (featured.RequiresTicketVerification ? 148 : 112) - 7,
                CreatedAt: DateTime.UtcNow.AddDays(-1),
                IsTicketVerified: featured.RequiresTicketVerification,
                ContextLabel: featured.Title,
                Location: "Sai Gon"),
            new ReviewSnapshot(
                Id: "home-rv-2",
                Author: "Minh Tân",
                Username: BuildUsername("Minh Tân"),
                AvatarUrl: "https://i.pravatar.cc/96?img=21",
                BadgeLabel: "Thành viên kỳ cựu",
                Excerpt: "Âm thanh và hình ảnh đều vượt kỳ vọng, nên đặt vé suất IMAX.",
                Rating: 8,
                FairVotes: 106,
                UnfairVotes: 11,
                SupportScore: 106 - 11,
                CreatedAt: DateTime.UtcNow.AddDays(-3),
                IsTicketVerified: featured.RequiresTicketVerification,
                ContextLabel: featured.Title,
                Location: "Da Nang"),
            new ReviewSnapshot(
                Id: "home-rv-3",
                Author: "Ngọc Anh",
                Username: BuildUsername("Ngọc Anh"),
                AvatarUrl: "https://i.pravatar.cc/96?img=9",
                BadgeLabel: "Cộng đồng CineReview",
                Excerpt: "Câu chuyện chạm tới cảm xúc, phù hợp xem cùng bạn bè.",
                Rating: 8,
                FairVotes: 88,
                UnfairVotes: 10,
                SupportScore: 88 - 10,
                CreatedAt: DateTime.UtcNow.AddDays(-6),
                IsTicketVerified: false,
                ContextLabel: "Cộng đồng CineReview",
                Location: "Hai Phong"),
            new ReviewSnapshot(
                Id: "home-rv-4",
                Author: "Quang Huy",
                Username: BuildUsername("Quang Huy"),
                AvatarUrl: "https://i.pravatar.cc/96?img=60",
                BadgeLabel: "Ticket Verified",
                Excerpt: "Xác thực vé cực nhanh, mình đã đăng review chỉ sau 5 phút.",
                Rating: 10,
                FairVotes: 132,
                UnfairVotes: 4,
                SupportScore: 132 - 4,
                CreatedAt: DateTime.UtcNow.AddDays(-8),
                IsTicketVerified: true,
                ContextLabel: "Ticket Verified",
                Location: "Can Tho")
        };
    }

    private static IReadOnlyList<EditorialSpotlight> BuildEditorialSpotlights()
    {
        return new[]
        {
            new EditorialSpotlight(
                Title: "Tổng hợp suất IMAX tháng này",
                Description: "Các bộ phim bom tấn đang chiếu IMAX cùng mẹo đặt vé sớm.",
                ImageUrl: "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=1200&q=80",
                ActionLabel: "Đọc bài viết",
                ActionUrl: "#"),
            new EditorialSpotlight(
                Title: "Tips xác thực vé điện tử nhanh",
                Description: "Hướng dẫn cập nhật vé từ các cụm rạp phổ biến tại Việt Nam.",
                ImageUrl: "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=1200&q=80",
                ActionLabel: "Xem ngay",
                ActionUrl: "#")
        };
    }

    private string ResolveImageUrl(string? path, string size)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "https://placehold.co/500x750?text=No+Image";
        }

        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.ImageBaseUrl)
            ? "https://image.tmdb.org/t/p/"
            : _options.ImageBaseUrl.TrimEnd('/') + '/';

        return $"{baseUrl}{size}{path}";
    }

    private static string? ExtractCertification(TmdbMovieDetail detail)
    {
        var results = detail.ReleaseDates?.Results;
        if (results is null || results.Count == 0)
        {
            return null;
        }

        string? MatchRegion(string region)
        {
            var certification = results
                .FirstOrDefault(r => string.Equals(r.Region, region, StringComparison.OrdinalIgnoreCase))?
                .ReleaseDates?
                .Where(d => !string.IsNullOrWhiteSpace(d.Certification))
                .OrderBy(d => d.Type)
                .ThenByDescending(d => d.ReleaseDate)
                .FirstOrDefault()?.Certification;
            return string.IsNullOrWhiteSpace(certification) ? null : certification;
        }

        return MatchRegion("VN") ?? MatchRegion("US") ?? MatchRegion("GB") ?? MatchRegion("CA");
    }

    private sealed record TmdbGenreResponse
    {
        [JsonPropertyName("genres")]
        public List<TmdbGenre>? Genres { get; init; }
    }

    private sealed record TmdbCountry
    {
        [JsonPropertyName("iso_3166_1")]
        public string? Iso3166 { get; init; }

        [JsonPropertyName("english_name")]
        public string? EnglishName { get; init; }
    }

    private sealed record TmdbGenre
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }

    private sealed record TmdbMovieListResponse
    {
        [JsonPropertyName("page")]
        public int Page { get; init; }

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; init; }

        [JsonPropertyName("total_results")]
        public int TotalResults { get; init; }

        [JsonPropertyName("results")]
        public List<TmdbMovieListItem> Results { get; init; } = new();
    }

    private sealed record TmdbMovieListItem
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("original_title")]
        public string? OriginalTitle { get; init; }

        [JsonPropertyName("overview")]
        public string? Overview { get; init; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; init; }

        [JsonPropertyName("backdrop_path")]
        public string? BackdropPath { get; init; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; init; }

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; init; }

        [JsonPropertyName("popularity")]
        public double Popularity { get; init; }

        [JsonPropertyName("genre_ids")]
        public List<int>? GenreIds { get; init; }
    }

    private sealed record TmdbMovieDetail
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("original_title")]
        public string? OriginalTitle { get; init; }

        [JsonPropertyName("overview")]
        public string? Overview { get; init; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; init; }

        [JsonPropertyName("backdrop_path")]
        public string? BackdropPath { get; init; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; init; }

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; init; }

        [JsonPropertyName("popularity")]
        public double Popularity { get; init; }

        [JsonPropertyName("genre_ids")]
        public List<int>? GenreIds { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("tagline")]
        public string? Tagline { get; init; }

        [JsonPropertyName("runtime")]
        public int? Runtime { get; init; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; init; }

        [JsonPropertyName("genres")]
        public List<TmdbGenre>? Genres { get; init; }

        [JsonPropertyName("credits")]
        public TmdbCreditResponse? Credits { get; init; }

        [JsonPropertyName("videos")]
        public TmdbVideoResponse? Videos { get; init; }

        [JsonPropertyName("recommendations")]
        public TmdbMovieListResponse? Recommendations { get; init; }

        [JsonPropertyName("release_dates")]
        public TmdbReleaseDatesResponse? ReleaseDates { get; init; }
    }

    private sealed record TmdbCreditResponse
    {
        [JsonPropertyName("cast")]
        public List<TmdbCastMember> Cast { get; init; } = new();
    }

    private sealed record TmdbCastMember
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("character")]
        public string? Character { get; init; }

        [JsonPropertyName("profile_path")]
        public string? ProfilePath { get; init; }

        [JsonPropertyName("order")]
        public int Order { get; init; }

        [JsonPropertyName("popularity")]
        public double Popularity { get; init; }
    }

    private sealed record TmdbVideoResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbVideoItem> Results { get; init; } = new();
    }

    private sealed record TmdbVideoItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("key")]
        public string? Key { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("site")]
        public string? Site { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; init; }
    }

    private sealed record TmdbReleaseDatesResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbReleaseDatesResult> Results { get; init; } = new();
    }

    private sealed record TmdbReleaseDatesResult
    {
        [JsonPropertyName("iso_3166_1")]
        public string Region { get; init; } = string.Empty;

        [JsonPropertyName("release_dates")]
        public List<TmdbReleaseDateInfo>? ReleaseDates { get; init; }
    }

    private sealed record TmdbReleaseDateInfo
    {
        [JsonPropertyName("certification")]
        public string? Certification { get; init; }

        [JsonPropertyName("type")]
        public int Type { get; init; }

        [JsonPropertyName("release_date")]
        public DateTime? ReleaseDate { get; init; }
    }
}
