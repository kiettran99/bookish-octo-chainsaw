using System;

namespace CineReview.Client.Features.Users;

public sealed class CineReviewApiOptions
{
    public const string SectionName = "CineReviewApi";

    public string? BaseUrl { get; set; }

    public TimeSpan ProfileCacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}
