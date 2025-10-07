using System;
using System.Collections.Generic;

namespace Common.Models;

/// <summary>
/// Represents a paginated list of items with pagination metadata.
/// </summary>
/// <typeparam name="T">Type of the items in the collection.</typeparam>
public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items ?? Array.Empty<T>();
        Page = page < 1 ? 1 : page;
        PageSize = pageSize < 1 ? 1 : pageSize;
        TotalCount = totalCount < 0 ? 0 : totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
