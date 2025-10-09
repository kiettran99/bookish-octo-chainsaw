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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CineReview.Client.Features.Movies;

public sealed class TmdbMovieDataProvider : IMovieDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly TmdbOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TmdbMovieDataProvider> _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    private const string GenreCacheKey = "tmdb:genres:v1";
    private const string CountryCacheKey = "tmdb:countries:v1";
    private const string DefaultOverviewFallback = "Thông tin nội dung đang được cập nhật.";

    private static readonly string[] PreferredVideoTypes = { "Trailer", "Teaser", "Clip", "Featurette" };
    private static readonly string[] RestrictedVideoKeywords =
    {
        "red band",
        "restricted",
        "age-restricted",
        "explicit",
        "uncensored",
        "nsfw",
        "18+",
        "18 plus",
        "unrated",
        "adult",
        "age restricted",
        "uncut"
    };

    private IReadOnlyList<string>? _preferredLanguageCodes;

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
            var detailedSummary = await GetMovieSummaryWithDetailAsync(featuredCandidate.Id, genreLookup, null, cancellationToken).ConfigureAwait(false);
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
        var detail = await GetMovieDetailPayloadAsync(id, includeExtended: true, languageOverride: null, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            return null;
        }

        var needsVideoFallback = !HasSafeVideos(detail.Videos);

        if (needsVideoFallback && !string.Equals(_options.DefaultLanguage, "en-US", StringComparison.OrdinalIgnoreCase))
        {
            var fallbackDetail = await GetMovieDetailPayloadAsync(id, includeExtended: true, languageOverride: "en-US", cancellationToken).ConfigureAwait(false);
            if (fallbackDetail is not null)
            {
                detail = MergeDetailFallback(detail, fallbackDetail);
            }
        }

        var summary = MapToSummary(detail, genreLookup, inferIsNowPlaying: null);
        var certification = ExtractCertification(detail) ?? "Đang cập nhật";
        var highlights = BuildHighlights(detail, summary, certification);
        var watchOptions = BuildWatchOptions(detail, summary);
        var ticketPolicy = BuildTicketPolicy(summary);
        var cast = BuildTopCast(detail);

        var videos = BuildVideos(detail);
        if (videos.Count == 0)
        {
            var supplementalVideos = await FetchSupplementalVideosAsync(id, detail.Videos?.Results, cancellationToken).ConfigureAwait(false);
            videos = BuildVideos(detail, supplementalVideos);
        }

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
        var enrichmentIds = new List<int>();
        if (payload.Results is not null)
        {
            foreach (var item in payload.Results)
            {
                if (item is null)
                {
                    continue;
                }

                if (item.Adult)
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

                if (NeedsDetailEnrichment(item))
                {
                    enrichmentIds.Add(item.Id);
                }
            }
        }

        if (enrichmentIds.Count > 0)
        {
            var enrichedLookup = await FetchEnrichedSummariesAsync(enrichmentIds, genreLookup, isNowPlaying: null, cancellationToken).ConfigureAwait(false);
            if (enrichedLookup.Count > 0)
            {
                for (var i = 0; i < summaries.Count; i++)
                {
                    if (enrichedLookup.TryGetValue(summaries[i].Id, out var enriched))
                    {
                        summaries[i] = enriched;
                    }
                }
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
        var payload = await GetFromTmdbAsync<TmdbMovieListResponse>(path, new Dictionary<string, string?>
        {
            ["include_adult"] = "false"
        }, cancellationToken).ConfigureAwait(false);
        if (payload?.Results is null || payload.Results.Count == 0)
        {
            return Array.Empty<MovieSummary>();
        }

        var selected = payload.Results
            .Where(item => !item.Adult)
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .OrderByDescending(item => item.Popularity)
            .Take(take)
            .ToArray();

        var summaries = new List<MovieSummary>(selected.Length);
        foreach (var item in selected)
        {
            var summary = await GetMovieSummaryWithDetailAsync(item.Id, genreLookup, isNowPlaying, cancellationToken).ConfigureAwait(false);
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

    private async Task<MovieSummary?> GetMovieSummaryWithDetailAsync(int id, IDictionary<int, string> genreLookup, bool? isNowPlayingHint, CancellationToken cancellationToken)
    {
        var detail = await GetMovieDetailPayloadAsync(id, includeExtended: false, languageOverride: null, cancellationToken).ConfigureAwait(false);
        if (detail is null || detail.Adult)
        {
            return null;
        }

        return MapToSummary(detail, genreLookup, inferIsNowPlaying: isNowPlayingHint);
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
            ["page"] = safePage.ToString(CultureInfo.InvariantCulture),
            ["include_adult"] = "false"
        };

        var genreLookup = await GetGenreLookupAsync(cancellationToken).ConfigureAwait(false);
        var payload = await GetFromTmdbAsync<TmdbMovieListResponse>(path, query, cancellationToken).ConfigureAwait(false);

        if (payload?.Results is null || payload.Results.Count == 0)
        {
            var totalPagesFallback = payload is null || payload.TotalPages <= 0 ? 1 : payload.TotalPages;
            return new PaginatedMovies(Array.Empty<MovieSummary>(), safePage, totalPagesFallback, payload?.TotalResults ?? 0, title, description);
        }

        var summaries = new List<MovieSummary>(payload.Results.Count);
        var enrichmentIds = new List<int>();
        foreach (var item in payload.Results)
        {
            if (item.Adult)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Title) && string.IsNullOrWhiteSpace(item.OriginalTitle))
            {
                continue;
            }

            var summary = MapToSummary(item, genreLookup, isNowPlaying);
            summaries.Add(summary);

            if (NeedsDetailEnrichment(item))
            {
                enrichmentIds.Add(item.Id);
            }
        }

        if (enrichmentIds.Count > 0)
        {
            var enrichedLookup = await FetchEnrichedSummariesAsync(enrichmentIds, genreLookup, isNowPlaying, cancellationToken).ConfigureAwait(false);
            if (enrichedLookup.Count > 0)
            {
                for (var i = 0; i < summaries.Count; i++)
                {
                    if (enrichedLookup.TryGetValue(summaries[i].Id, out var enriched))
                    {
                        summaries[i] = enriched;
                    }
                }
            }
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

        var resolvedTitle = ResolveTitle(detail);
        var resolvedTagline = ResolveTagline(detail);
        var resolvedOverview = ResolveOverview(detail);

        return new MovieSummary(
            Id: detail.Id,
            Title: resolvedTitle,
            Tagline: resolvedTagline,
            PosterUrl: ResolveImageUrl(detail.PosterPath, _options.PosterSize),
            BackdropUrl: ResolveImageUrl(detail.BackdropPath, _options.BackdropSize),
            ReleaseDate: releaseDate,
            CommunityScore: detail.VoteAverage,
            Genres: genres,
            Overview: resolvedOverview,
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

        var overview = string.IsNullOrWhiteSpace(item.Overview) ? DefaultOverviewFallback : item.Overview!;

        return new MovieSummary(
            Id: item.Id,
            Title: item.Title ?? item.OriginalTitle ?? "Tên phim chưa cập nhật",
            Tagline: string.Empty,
            PosterUrl: ResolveImageUrl(item.PosterPath, _options.PosterSize),
            BackdropUrl: ResolveImageUrl(item.BackdropPath, _options.BackdropSize),
            ReleaseDate: releaseDate,
            CommunityScore: item.VoteAverage,
            Genres: genres,
            Overview: overview,
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

    private async Task<TmdbMovieDetail?> GetMovieDetailPayloadAsync(int id, bool includeExtended, string? languageOverride, CancellationToken cancellationToken)
    {
        var languageKey = string.IsNullOrWhiteSpace(languageOverride)
            ? string.IsNullOrWhiteSpace(_options.DefaultLanguage) ? "default" : _options.DefaultLanguage!
            : languageOverride;

        var cacheKey = includeExtended
            ? $"tmdb:movie:{id}:full:{languageKey}"
            : $"tmdb:movie:{id}:core:{languageKey}";
        if (_cache.TryGetValue(cacheKey, out object? cachedObj) && cachedObj is TmdbMovieDetail cachedDetail)
        {
            return cachedDetail;
        }

        var query = includeExtended
            ? new Dictionary<string, string?>
            {
                ["append_to_response"] = "credits,videos,recommendations,release_dates,translations",
                ["include_image_language"] = BuildIncludeImageLanguageParameter(),
                ["include_video_language"] = BuildIncludeVideoLanguageParameter()
            }
            : new Dictionary<string, string?>
            {
                ["append_to_response"] = "translations",
                ["include_image_language"] = BuildIncludeImageLanguageParameter(),
                ["include_video_language"] = BuildIncludeVideoLanguageParameter()
            };

        if (languageOverride is not null)
        {
            query["language"] = languageOverride;
        }

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
        var hasLanguageParam = false;
        var hasRegionParam = false;

        if (query is not null)
        {
            foreach (var kv in query)
            {
                if (string.Equals(kv.Key, "language", StringComparison.OrdinalIgnoreCase))
                {
                    hasLanguageParam = true;
                }

                if (string.Equals(kv.Key, "region", StringComparison.OrdinalIgnoreCase))
                {
                    hasRegionParam = true;
                }
            }

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

        if (!hasLanguageParam && !string.IsNullOrWhiteSpace(_options.DefaultLanguage))
        {
            queryParameters.Add($"language={Uri.EscapeDataString(_options.DefaultLanguage)}");
        }

        if (!hasRegionParam && !string.IsNullOrWhiteSpace(_options.DefaultRegion))
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

    private static bool HasSafeVideos(TmdbVideoResponse? videos)
        => videos?.Results?.Any(IsSafeVideo) == true;

    private static TmdbMovieDetail MergeDetailFallback(TmdbMovieDetail primary, TmdbMovieDetail fallback)
    {
        return primary with
        {
            // Không merge Overview - chỉ dùng overview tiếng Việt
            Tagline = string.IsNullOrWhiteSpace(primary.Tagline) ? fallback.Tagline : primary.Tagline,
            PosterPath = string.IsNullOrWhiteSpace(primary.PosterPath) ? fallback.PosterPath : primary.PosterPath,
            BackdropPath = string.IsNullOrWhiteSpace(primary.BackdropPath) ? fallback.BackdropPath : primary.BackdropPath,
            Runtime = primary.Runtime ?? fallback.Runtime,
            Status = string.IsNullOrWhiteSpace(primary.Status) ? fallback.Status : primary.Status,
            Genres = primary.Genres is { Count: > 0 } ? primary.Genres : fallback.Genres ?? primary.Genres,
            GenreIds = primary.GenreIds is { Count: > 0 } ? primary.GenreIds : fallback.GenreIds ?? primary.GenreIds,
            ReleaseDates = primary.ReleaseDates is { Results.Count: > 0 } ? primary.ReleaseDates : fallback.ReleaseDates ?? primary.ReleaseDates,
            Credits = primary.Credits is { Cast.Count: > 0 } ? primary.Credits : fallback.Credits ?? primary.Credits,
            Recommendations = primary.Recommendations is { Results.Count: > 0 } ? primary.Recommendations : fallback.Recommendations ?? primary.Recommendations,
            Translations = primary.Translations is { Translations.Count: > 0 } ? primary.Translations : fallback.Translations ?? primary.Translations,
            Videos = CombineVideoResponses(primary.Videos, fallback.Videos)
        };
    }

    private static TmdbVideoResponse? CombineVideoResponses(TmdbVideoResponse? primary, TmdbVideoResponse? secondary)
    {
        var primaryCount = primary?.Results?.Count ?? 0;
        var secondaryCount = secondary?.Results?.Count ?? 0;
        if (primaryCount == 0 && secondaryCount == 0)
        {
            return primary ?? secondary;
        }

        var combined = new List<TmdbVideoItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Append(TmdbVideoResponse? source)
        {
            if (source?.Results is null)
            {
                return;
            }

            foreach (var video in source.Results)
            {
                if (video?.Key is null)
                {
                    continue;
                }

                if (seen.Add(video.Key))
                {
                    combined.Add(video);
                }
            }
        }

        Append(primary);
        Append(secondary);

        return new TmdbVideoResponse { Results = combined };
    }

    private async Task<IReadOnlyList<TmdbVideoItem>> FetchSupplementalVideosAsync(int id, IEnumerable<TmdbVideoItem>? existingVideos, CancellationToken cancellationToken)
    {
        var aggregated = new List<TmdbVideoItem>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (existingVideos is not null)
        {
            foreach (var video in existingVideos)
            {
                if (video?.Key is not null)
                {
                    seenKeys.Add(video.Key);
                }
            }
        }

        var languages = new List<string?>();
        var languageSet = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);

        void EnqueueLanguage(string? value)
        {
            if (languageSet.Add(value))
            {
                languages.Add(value);
            }
        }

        EnqueueLanguage(null);
        EnqueueLanguage("en-US");
        EnqueueLanguage("en");

        foreach (var code in GetPreferredLanguageCodes())
        {
            EnqueueLanguage(code);
            if (!string.IsNullOrWhiteSpace(code) && code.Contains('-'))
            {
                var baseCode = code[..code.IndexOf('-')];
                EnqueueLanguage(baseCode);
            }
        }

        foreach (var language in languages)
        {
            if (!string.IsNullOrWhiteSpace(_options.DefaultLanguage) && string.Equals(language, _options.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var query = new Dictionary<string, string?>
            {
                ["include_video_language"] = BuildIncludeVideoLanguageParameter()
            };

            query["language"] = language;

            var payload = await GetFromTmdbAsync<TmdbVideoResponse>($"movie/{id}/videos", query, cancellationToken).ConfigureAwait(false);
            if (payload?.Results is null || payload.Results.Count == 0)
            {
                continue;
            }

            foreach (var video in payload.Results)
            {
                if (video?.Key is null)
                {
                    continue;
                }

                if (!seenKeys.Add(video.Key))
                {
                    continue;
                }

                aggregated.Add(video);
            }

            if (aggregated.Count >= 8)
            {
                break;
            }
        }

        return aggregated;
    }

    private static IReadOnlyList<TrailerVideo> BuildVideos(TmdbMovieDetail detail, IEnumerable<TmdbVideoItem>? supplementalVideos = null)
    {
        if (detail.Adult)
        {
            return Array.Empty<TrailerVideo>();
        }

        var candidates = new List<TmdbVideoItem>();

        void Append(IEnumerable<TmdbVideoItem>? source)
        {
            if (source is null)
            {
                return;
            }

            candidates.AddRange(source.Where(item => item is not null));
        }

        Append(detail.Videos?.Results);
        Append(supplementalVideos);

        if (candidates.Count == 0)
        {
            return Array.Empty<TrailerVideo>();
        }

        return candidates
            .Where(IsSafeVideo)
            .GroupBy(video => video.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(video => video.Official)
            .ThenBy(GetVideoTypePriority)
            .ThenByDescending(video => video.PublishedAt ?? DateTime.MinValue)
            .Take(6)
            .Select(video => new TrailerVideo(
                Id: video.Id ?? video.Key!,
                Title: video.Name ?? "Video chính thức",
                ThumbnailUrl: BuildYouTubeThumbnail(video.Key!),
                VideoUrl: $"https://www.youtube.com/watch?v={video.Key}",
                EmbedUrl: $"https://www.youtube.com/embed/{video.Key}?autoplay=1",
                Provider: "YouTube",
                IsOfficial: video.Official,
                Type: video.Type ?? string.Empty))
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
            .Where(item => !item.Adult)
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .OrderByDescending(item => item.Popularity)
            .Take(6)
            .Select(item => MapToSummary(item, genreLookup, isNowPlaying: null))
            .ToArray();
    }

    private async Task<Dictionary<int, MovieSummary>> FetchEnrichedSummariesAsync(IEnumerable<int> ids, IDictionary<int, string> genreLookup, bool? isNowPlaying, CancellationToken cancellationToken)
    {
        var seen = new HashSet<int>();
        var orderedIds = new List<int>();

        foreach (var id in ids)
        {
            if (seen.Add(id))
            {
                orderedIds.Add(id);
            }
        }

        if (orderedIds.Count == 0)
        {
            return new Dictionary<int, MovieSummary>();
        }

        var tasks = orderedIds
            .Select(id => GetMovieSummaryWithDetailAsync(id, genreLookup, isNowPlaying, cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return FetchEnrichedSummariesResultToDictionary(orderedIds, results);
    }

    private static Dictionary<int, MovieSummary> FetchEnrichedSummariesResultToDictionary(IReadOnlyList<int> ids, MovieSummary?[] results)
    {
        var map = new Dictionary<int, MovieSummary>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            var summary = results[i];
            if (summary is not null)
            {
                map[ids[i]] = summary;
            }
        }

        return map;
    }

    private static bool NeedsDetailEnrichment(TmdbMovieListItem item)
    {
        if (item is null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(item.Overview)
            || string.IsNullOrWhiteSpace(item.PosterPath)
            || string.IsNullOrWhiteSpace(item.BackdropPath);
    }

    private static string ResolveTagline(TmdbMovieDetail detail)
    {
        if (!string.IsNullOrWhiteSpace(detail.Tagline))
        {
            return detail.Tagline;
        }

        var translation = ResolveVietnameseTranslation(detail, data => data?.Tagline);
        return translation ?? string.Empty;
    }

    private static string ResolveOverview(TmdbMovieDetail detail)
    {
        if (!string.IsNullOrWhiteSpace(detail.Overview))
        {
            return detail.Overview;
        }

        // Chỉ lấy overview tiếng Việt, không fallback sang tiếng Anh
        var translation = ResolveVietnameseTranslation(detail, data => data?.Overview);
        return string.IsNullOrWhiteSpace(translation) ? DefaultOverviewFallback : translation!;
    }

    private static string? ResolveVietnameseTranslation(TmdbMovieDetail detail, Func<TmdbTranslationData?, string?> selector)
    {
        var translations = detail.Translations?.Translations;
        if (translations is null || translations.Count == 0)
        {
            return null;
        }

        // Ưu tiên vi-VN, sau đó vi
        foreach (var translation in translations)
        {
            if (translation.Data is null)
                continue;

            var isVietnamese = string.Equals(translation.Iso639, "vi", StringComparison.OrdinalIgnoreCase);
            if (!isVietnamese)
                continue;

            var value = selector(translation.Data);
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Ưu tiên vi-VN nếu có cả region
                if (string.Equals(translation.Iso3166, "VN", StringComparison.OrdinalIgnoreCase))
                    return value;

                // Lưu lại giá trị vi (không có region) để dùng nếu không tìm thấy vi-VN
                if (string.IsNullOrWhiteSpace(translation.Iso3166))
                    return value;
            }
        }

        return null;
    }

    private static string ResolveTitle(TmdbMovieDetail detail)
    {
        if (!string.IsNullOrWhiteSpace(detail.Title))
        {
            return detail.Title;
        }

        if (!string.IsNullOrWhiteSpace(detail.OriginalTitle))
        {
            return detail.OriginalTitle;
        }

        var translation = ResolveFromTranslations(detail, data => data?.Title);
        return string.IsNullOrWhiteSpace(translation) ? "Tên phim chưa cập nhật" : translation!;
    }

    private static string? ResolveFromTranslations(TmdbMovieDetail detail, Func<TmdbTranslationData?, string?> selector)
    {
        var translations = detail.Translations?.Translations;
        if (translations is null || translations.Count == 0)
        {
            return null;
        }

        return translations
            .Where(t => t.Data is not null)
            .OrderBy(GetTranslationPriority)
            .Select(t => selector(t.Data))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static int GetTranslationPriority(TmdbTranslation translation)
    {
        var languages = GetPreferredLanguageCodesStatic();
        var locale = CombineLocale(translation.Iso639, translation.Iso3166);

        for (var i = 0; i < languages.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(locale) && string.Equals(locale, languages[i], StringComparison.OrdinalIgnoreCase))
            {
                return i * 2;
            }

            if (!string.IsNullOrWhiteSpace(translation.Iso639) && string.Equals(translation.Iso639, languages[i], StringComparison.OrdinalIgnoreCase))
            {
                return i * 2 + 1;
            }
        }

        return languages.Count * 2 + 1;
    }

    private static string CombineLocale(string? language, string? region)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(region) ? language : $"{language}-{region}";
    }

    private static IReadOnlyList<string> GetPreferredLanguageCodesStatic()
    {
        // Vietnamese prioritized because the UI is Vietnamese-first.
        return new[] { "vi-VN", "vi", "en-US", "en" };
    }

    private IReadOnlyList<string> GetPreferredLanguageCodes()
    {
        if (_preferredLanguageCodes is not null)
        {
            return _preferredLanguageCodes;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (seen.Add(value))
            {
                ordered.Add(value);
            }
        }

        Add(_options.DefaultLanguage);

        if (!string.IsNullOrWhiteSpace(_options.DefaultLanguage) && _options.DefaultLanguage.Contains('-'))
        {
            Add(_options.DefaultLanguage[.._options.DefaultLanguage.IndexOf('-')]);
        }

        foreach (var locale in GetPreferredLanguageCodesStatic())
        {
            Add(locale);
        }

        _preferredLanguageCodes = ordered;
        return ordered;
    }

    private string BuildIncludeVideoLanguageParameter()
    {
        var preferred = GetPreferredLanguageCodes();
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in preferred)
        {
            if (seen.Add(code))
            {
                ordered.Add(code);
            }
        }

        if (seen.Add("null"))
        {
            ordered.Add("null");
        }

        return string.Join(',', ordered);
    }

    private string BuildIncludeImageLanguageParameter()
    {
        var preferred = GetPreferredLanguageCodes();
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in preferred)
        {
            if (seen.Add(code))
            {
                ordered.Add(code);
            }
        }

        if (seen.Add("null"))
        {
            ordered.Add("null");
        }

        return string.Join(',', ordered);
    }

    private static bool IsSafeVideo(TmdbVideoItem? video)
    {
        if (video is null || string.IsNullOrWhiteSpace(video.Key))
        {
            return false;
        }

        if (!string.Equals(video.Site, "YouTube", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsPreferredLanguage(video.Iso639))
        {
            return false;
        }

        var title = video.Name ?? string.Empty;
        foreach (var keyword in RestrictedVideoKeywords)
        {
            if (title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
        }

        if (video.Official)
        {
            return true;
        }

        return IsPreferredVideoType(video.Type);
    }

    private static bool IsPreferredLanguage(string? iso639)
    {
        if (string.IsNullOrWhiteSpace(iso639))
        {
            return true;
        }

        return string.Equals(iso639, "vi", StringComparison.OrdinalIgnoreCase)
            || string.Equals(iso639, "en", StringComparison.OrdinalIgnoreCase)
            || string.Equals(iso639, "xx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreferredVideoType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        return PreferredVideoTypes.Any(allowed => string.Equals(allowed, type, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetVideoTypePriority(TmdbVideoItem video)
    {
        if (string.IsNullOrWhiteSpace(video.Type))
        {
            return PreferredVideoTypes.Length + 1;
        }

        for (var i = 0; i < PreferredVideoTypes.Length; i++)
        {
            if (string.Equals(video.Type, PreferredVideoTypes[i], StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return PreferredVideoTypes.Length;
    }

    private static string BuildYouTubeThumbnail(string key)
        => $"https://img.youtube.com/vi/{key}/hqdefault.jpg";

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

        [JsonPropertyName("adult")]
        public bool Adult { get; init; }

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

        [JsonPropertyName("adult")]
        public bool Adult { get; init; }

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

        [JsonPropertyName("translations")]
        public TmdbTranslationsResponse? Translations { get; init; }
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

        [JsonPropertyName("official")]
        public bool Official { get; init; }

        [JsonPropertyName("iso_639_1")]
        public string? Iso639 { get; init; }

        [JsonPropertyName("iso_3166_1")]
        public string? Iso3166 { get; init; }

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

    private sealed record TmdbTranslationsResponse
    {
        [JsonPropertyName("translations")]
        public List<TmdbTranslation> Translations { get; init; } = new();
    }

    private sealed record TmdbTranslation
    {
        [JsonPropertyName("iso_639_1")]
        public string? Iso639 { get; init; }

        [JsonPropertyName("iso_3166_1")]
        public string? Iso3166 { get; init; }

        [JsonPropertyName("data")]
        public TmdbTranslationData? Data { get; init; }
    }

    private sealed record TmdbTranslationData
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("overview")]
        public string? Overview { get; init; }

        [JsonPropertyName("tagline")]
        public string? Tagline { get; init; }
    }
}
