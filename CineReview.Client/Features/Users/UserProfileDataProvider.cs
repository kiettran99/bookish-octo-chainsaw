using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CineReview.Client.Features.Movies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CineReview.Client.Features.Users;

public sealed class UserProfileDataProvider : IUserProfileDataProvider
{
    private const string ProfileCacheKeyPrefix = "user-profile:";
    private const int DefaultReviewPageSize = 10;

    private readonly HttpClient _httpClient;
    private readonly IMovieDataProvider _movieDataProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserProfileDataProvider> _logger;
    private readonly CineReviewApiOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;

    public UserProfileDataProvider(
        HttpClient httpClient,
        IMovieDataProvider movieDataProvider,
        IMemoryCache cache,
        ILogger<UserProfileDataProvider> logger,
        IOptions<CineReviewApiOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _movieDataProvider = movieDataProvider ?? throw new ArgumentNullException(nameof(movieDataProvider));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl, UriKind.Absolute);
        }

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<UserProfilePageViewModel> GetProfileAsync(string userName, int page, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return UserProfilePageViewModel.CreateError(string.Empty, "Thiếu thông tin tài khoản.");
        }

        if (_httpClient.BaseAddress is null)
        {
            _logger.LogWarning("CineReviewApi:BaseUrl chưa được cấu hình, không thể tải hồ sơ người dùng.");
            return UserProfilePageViewModel.CreateError(userName.Trim(), "Không thể tải hồ sơ vì thiếu cấu hình API.");
        }

        var sanitizedPage = page < 1 ? 1 : page;
        var normalizedUserName = userName.Trim();

        try
        {
            var profile = await GetProfileSummaryAsync(normalizedUserName, cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                return UserProfilePageViewModel.CreateNotFound(normalizedUserName);
            }

            var reviews = await GetReviewsAsync(profile.Id, sanitizedPage, cancellationToken).ConfigureAwait(false);
            return UserProfilePageViewModel.CreateSuccess(profile, reviews);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải hồ sơ cho người dùng {UserName}", normalizedUserName);
            return UserProfilePageViewModel.CreateError(normalizedUserName, "Không thể tải hồ sơ người dùng. Vui lòng thử lại sau.");
        }
    }

    private async Task<UserProfileSummary?> GetProfileSummaryAsync(string userName, CancellationToken cancellationToken)
    {
        var cacheKey = ProfileCacheKeyPrefix + userName.ToLowerInvariant();

        if (_cache.TryGetValue<UserProfileSummary>(cacheKey, out var cachedProfile) && cachedProfile is not null)
        {
            return cachedProfile;
        }

        using var response = await _httpClient.GetAsync($"api/user/{Uri.EscapeDataString(userName)}", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ServiceResponseDto<UserProfileDto>>(_serializerOptions, cancellationToken).ConfigureAwait(false);
        if (payload is null || !payload.IsSuccess || payload.Data is null)
        {
            var message = payload?.ErrorMessage ?? "API không trả về dữ liệu hợp lệ";
            throw new InvalidOperationException(message);
        }

        if (payload.Data.IsDeleted)
        {
            return null;
        }

        var badge = BuildBadge(payload.Data.CommunicationScore);
        var statsDto = payload.Data.ReviewStats ?? new UserReviewStatsDto();
        var stats = new UserReviewStatsViewModel(
            statsDto.TotalReviews,
            statsDto.ReleasedReviews,
            statsDto.PendingReviews,
            statsDto.TagReviews,
            statsDto.FreeformReviews,
            statsDto.AverageRating);

        var profile = new UserProfileSummary(
            payload.Data.Id,
            payload.Data.UserName,
            DetermineDisplayName(payload.Data.UserName, payload.Data.FullName),
            payload.Data.Email,
            payload.Data.Avatar,
            DateTime.SpecifyKind(payload.Data.CreatedOnUtc, DateTimeKind.Utc),
            payload.Data.ExpriedRoleDate,
            payload.Data.CommunicationScore,
            payload.Data.IsBanned,
            badge,
            stats);

        if (_options.ProfileCacheDuration > TimeSpan.Zero)
        {
            _cache.Set(cacheKey, profile, _options.ProfileCacheDuration);
        }

        return profile;
    }

    private async Task<UserReviewPage> GetReviewsAsync(int userId, int page, CancellationToken cancellationToken)
    {
        var url = $"api/review/user/{userId}?page={page}&pageSize={DefaultReviewPageSize}";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return UserReviewPage.Empty;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ServiceResponseDto<PagedResultDto<ReviewResponseDto>>>(_serializerOptions, cancellationToken).ConfigureAwait(false);
        if (payload is null || !payload.IsSuccess || payload.Data is null)
        {
            var message = payload?.ErrorMessage ?? "Không lấy được danh sách review";
            throw new InvalidOperationException(message);
        }

        var rawItems = payload.Data.Items ?? new List<ReviewResponseDto>();
        var reviewItems = rawItems
            .Select(item => new UserReviewViewModel(
                item.Id,
                item.TmdbMovieId,
                null,
                MapReviewType(item.Type),
                MapReviewStatus(item.Status),
                item.Rating,
                NormalizeDescription(item.Description),
                MapTagRatings(item.DescriptionTag),
                DateTime.SpecifyKind(item.CreatedOnUtc, DateTimeKind.Utc),
                item.UpdatedOnUtc,
                item.CommunicationScore))
            .ToList();

        var resolvedPage = payload.Data.Page <= 0 ? page : payload.Data.Page;
        var resolvedPageSize = payload.Data.PageSize <= 0 ? DefaultReviewPageSize : payload.Data.PageSize;
        var totalCount = payload.Data.TotalCount < 0 ? 0 : payload.Data.TotalCount;

        if (reviewItems.Count == 0)
        {
            return UserReviewPage.Create(reviewItems, resolvedPage, resolvedPageSize, totalCount);
        }

        var movieLookup = await FetchMovieSummariesAsync(reviewItems.Select(r => r.MovieId).Distinct(), cancellationToken).ConfigureAwait(false);
        var enriched = reviewItems
            .Select(review => movieLookup.TryGetValue(review.MovieId, out var movie)
                ? review with { Movie = movie }
                : review)
            .ToList();

        return UserReviewPage.Create(enriched, resolvedPage, resolvedPageSize, totalCount);
    }

    private async Task<Dictionary<int, UserReviewMovieSummary>> FetchMovieSummariesAsync(IEnumerable<int> movieIds, CancellationToken cancellationToken)
    {
        var summaries = new Dictionary<int, UserReviewMovieSummary>();
        foreach (var movieId in movieIds)
        {
            if (movieId <= 0 || summaries.ContainsKey(movieId))
            {
                continue;
            }

            try
            {
                var detail = await _movieDataProvider.GetMovieDetailAsync(movieId, cancellationToken).ConfigureAwait(false);
                if (detail?.Summary is null)
                {
                    continue;
                }

                var summary = detail.Summary;
                summaries[movieId] = new UserReviewMovieSummary(
                    summary.Id,
                    summary.Title,
                    summary.PosterUrl,
                    summary.ReleaseDate,
                    summary.CommunityScore,
                    summary.IsNowPlaying);
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

        return summaries;
    }

    private static ReviewerBadgeViewModel BuildBadge(long score)
    {
        if (score < -100)
        {
            return new ReviewerBadgeViewModel("Reviewer Toxic", "community-review__badge--danger", true);
        }

        if (score is >= -100 and <= -10)
        {
            return new ReviewerBadgeViewModel("Reviewer Chưa Công Tâm", "community-review__badge--warning", true);
        }

        if (score is >= -9 and <= 10)
        {
            return new ReviewerBadgeViewModel("Reviewer Vô Danh", "community-review__badge--secondary", true);
        }

        if (score is >= 11 and <= 100)
        {
            return new ReviewerBadgeViewModel("Reviewer Tập Sự", "community-review__badge--info", true);
        }

        if (score is >= 101 and <= 500)
        {
            return new ReviewerBadgeViewModel("Reviewer Có Tiếng", "community-review__badge--primary", true);
        }

        if (score > 500)
        {
            return new ReviewerBadgeViewModel("Reviewer Chuyên Nghiệp", "community-review__badge--success", true);
        }

        return new ReviewerBadgeViewModel("Reviewer Vô Danh", "community-review__badge--secondary", true);
    }

    private static string DetermineDisplayName(string userName, string? fullName)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName.Trim();
        }

        return userName;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        return trimmed.Length > 200 ? trimmed[..200] + "…" : trimmed;
    }

    private static IReadOnlyList<TagRatingViewModel> MapTagRatings(JsonElement? element)
    {
        if (!element.HasValue)
        {
            return Array.Empty<TagRatingViewModel>();
        }

        var value = element.Value;
        if (value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<TagRatingViewModel>();
        }

        var tags = new List<TagRatingViewModel>();
        foreach (var item in value.EnumerateArray())
        {
            var tagId = TryGetInt32(item, "tagId") ?? TryGetInt32(item, "TagId");
            if (!tagId.HasValue)
            {
                continue;
            }

            var tagName = TryGetString(item, "tagName") ?? TryGetString(item, "TagName") ?? $"Tag {tagId.Value}";
            var rating = TryGetInt32(item, "rating") ?? TryGetInt32(item, "Rating") ?? 0;
            tags.Add(new TagRatingViewModel(tagId.Value, tagName, rating));
        }

        return tags;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetInt32(out var number) => number,
                JsonValueKind.String when int.TryParse(property.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        return null;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static UserReviewType MapReviewType(int value)
        => value switch
        {
            0 => UserReviewType.Tag,
            1 => UserReviewType.Freeform,
            _ => UserReviewType.Freeform
        };

    private static UserReviewStatus MapReviewStatus(int value)
        => value switch
        {
            0 => UserReviewStatus.Pending,
            1 => UserReviewStatus.Released,
            2 => UserReviewStatus.Deleted,
            _ => UserReviewStatus.Pending
        };

    private sealed record ServiceResponseDto<T>(bool IsSuccess, string? ErrorMessage, T? Data);

    private sealed record UserProfileDto
    {
        public int Id { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? FullName { get; init; }
        public string Email { get; init; } = string.Empty;
        public string? Avatar { get; init; }
        public DateTime? ExpriedRoleDate { get; init; }
        public DateTime CreatedOnUtc { get; init; }
        public long CommunicationScore { get; init; }
        public int Region { get; init; }
        public bool IsBanned { get; init; }
        public bool IsDeleted { get; init; }
        public UserReviewStatsDto? ReviewStats { get; init; }
    }

    private sealed record UserReviewStatsDto
    {
        public int TotalReviews { get; init; }
        public int ReleasedReviews { get; init; }
        public int PendingReviews { get; init; }
        public int TagReviews { get; init; }
        public int FreeformReviews { get; init; }
        public double? AverageRating { get; init; }
    }

    private sealed record PagedResultDto<T>
    {
        public List<T> Items { get; init; } = new();
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
    }

    private sealed record ReviewResponseDto
    {
        public int Id { get; init; }
        public int UserId { get; init; }
        public int TmdbMovieId { get; init; }
        public int Status { get; init; }
        public int Type { get; init; }
        public JsonElement? DescriptionTag { get; init; }
        public string? Description { get; init; }
        public int Rating { get; init; }
        public long CommunicationScore { get; init; }
        public DateTime CreatedOnUtc { get; init; }
        public DateTime? UpdatedOnUtc { get; init; }
    }
}
