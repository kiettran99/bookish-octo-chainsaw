using System;
using System.Collections.Generic;

namespace CineReview.Models;

public sealed record MovieSummary(
    int Id,
    string Title,
    string Tagline,
    string PosterUrl,
    string BackdropUrl,
    DateTime ReleaseDate,
    double CommunityScore,
    IReadOnlyList<string> Genres,
    string Overview,
    bool IsNowPlaying,
    bool RequiresTicketVerification
);

public sealed record ReviewSnapshot(
    string Id,
    string Author,
    string AvatarUrl,
    string Excerpt,
    int Rating,
    DateTime CreatedAt,
    bool IsTicketVerified,
    string ContextLabel,
    string? Location = null
);

public sealed record EditorialSpotlight(
    string Title,
    string Description,
    string ImageUrl,
    string ActionLabel,
    string ActionUrl
);

public sealed record CastMember(
    string Name,
    string Character,
    string HeadshotUrl
);

public sealed record TrailerVideo(
    string Id,
    string Title,
    string ThumbnailUrl,
    string VideoUrl,
    string Provider
);

public sealed record MovieProfile(
    MovieSummary Summary,
    int RuntimeMinutes,
    string Status,
    string Certification,
    string TicketPolicyNote,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> WatchOptions,
    IReadOnlyList<CastMember> TopCast,
    IReadOnlyList<TrailerVideo> Videos,
    IReadOnlyList<ReviewSnapshot> Reviews,
    IReadOnlyList<MovieSummary> Recommended
);

public sealed record HomePageData(
    MovieSummary Featured,
    IReadOnlyList<MovieSummary> NowPlaying,
    IReadOnlyList<MovieSummary> ComingSoon,
    IReadOnlyList<MovieSummary> TrendingThisWeek,
    IReadOnlyList<ReviewSnapshot> LatestReviews,
    IReadOnlyList<EditorialSpotlight> EditorialSpots
);
