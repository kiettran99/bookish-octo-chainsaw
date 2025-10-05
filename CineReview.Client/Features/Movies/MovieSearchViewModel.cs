using System;

namespace CineReview.Client.Features.Movies;

public sealed class MovieSearchViewModel
{
    public required MovieSearchRequest Criteria { get; init; }

    public MovieSearchResult? Result { get; init; }

    public string? ErrorMessage { get; init; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
}
