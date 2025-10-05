using System;

namespace CineReview.Client.Features.Movies;

public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";

    public string ImageBaseUrl { get; set; } = "https://image.tmdb.org/t/p/";

    public string PosterSize { get; set; } = "w500";

    public string BackdropSize { get; set; } = "w1280";

    public string DefaultLanguage { get; set; } = "vi-VN";

    public string? DefaultRegion { get; set; } = "VN";

    public string? ApiKey { get; set; }

    public string? AccessToken { get; set; }

    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(15);
}
