using System;
using System.Collections.Generic;
using System.Linq;

namespace CineReview.Client.Features.Users;

public sealed class UserProfilePageViewModel
{
    private UserProfilePageViewModel(
        string userName,
        UserProfileSummary? profile,
        UserReviewPage reviews,
        bool notFound,
        string? errorMessage)
    {
        UserName = userName;
        Profile = profile;
        Reviews = reviews;
        NotFound = notFound;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage;
    }

    public string UserName { get; }

    public UserProfileSummary? Profile { get; }

    public UserReviewPage Reviews { get; }

    public bool NotFound { get; }

    public string? ErrorMessage { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage) && !NotFound;

    public bool HasData => Profile is not null;

    public static UserProfilePageViewModel CreateSuccess(UserProfileSummary profile, UserReviewPage reviews)
        => new(profile.UserName, profile, reviews, notFound: false, errorMessage: null);

    public static UserProfilePageViewModel CreateNotFound(string userName)
        => new(userName, null, UserReviewPage.Empty, notFound: true, errorMessage: null);

    public static UserProfilePageViewModel CreateError(string userName, string? message)
        => new(userName, null, UserReviewPage.Empty, notFound: false, errorMessage: message);
}

public sealed record UserProfileSummary(
    int Id,
    string UserName,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    DateTime CreatedOnUtc,
    DateTime? ExpriedRoleDate,
    long CommunicationScore,
    bool IsBanned,
    ReviewerBadgeViewModel Badge,
    UserReviewStatsViewModel ReviewStats
);

public sealed record ReviewerBadgeViewModel(string Label, string CssClass, bool ShouldDisplay);

public sealed record UserReviewStatsViewModel(
    int TotalReviews,
    int ReleasedReviews,
    int PendingReviews,
    int TagReviews,
    int FreeformReviews,
    double? AverageRating
)
{
    public double? AverageRatingRounded => AverageRating.HasValue
        ? Math.Round(AverageRating.Value, 1)
        : null;
};

public enum UserReviewType
{
    Tag = 0,
    Freeform = 1
}

public enum UserReviewStatus
{
    Pending = 0,
    Released = 1,
    Deleted = 2
}

public sealed record TagRatingViewModel(int TagId, string TagName, int Rating);

public sealed record UserReviewMovieSummary(
    int Id,
    string Title,
    string PosterUrl,
    DateTime ReleaseDate,
    double CommunityScore,
    bool IsNowPlaying
)
{
    public string ReleaseYear => ReleaseDate.Year.ToString();
};

public sealed record UserReviewViewModel(
    int Id,
    int MovieId,
    UserReviewMovieSummary? Movie,
    UserReviewType Type,
    UserReviewStatus Status,
    int Rating,
    string? Description,
    IReadOnlyList<TagRatingViewModel> Tags,
    DateTime CreatedOnUtc,
    DateTime? UpdatedOnUtc,
    long CommunicationScore
)
{
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasTags => Tags.Count > 0;

    public double? AverageTagRating => HasTags
        ? Math.Round(Tags.Average(tag => tag.Rating), 1)
        : null;
};

public sealed class UserReviewPage
{
    private UserReviewPage(IReadOnlyList<UserReviewViewModel> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page < 1 ? 1 : page;
        PageSize = pageSize < 1 ? 1 : pageSize;
        TotalCount = totalCount < 0 ? 0 : totalCount;
    }

    public IReadOnlyList<UserReviewViewModel> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static UserReviewPage Create(IReadOnlyList<UserReviewViewModel> items, int page, int pageSize, int totalCount)
        => new(items, page, pageSize, totalCount);

    public static UserReviewPage Empty { get; } = new UserReviewPage(Array.Empty<UserReviewViewModel>(), 1, 1, 0);
}
