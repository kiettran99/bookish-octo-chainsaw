using System.Linq.Expressions;

namespace Common.Helpers;

public static class QueryBuilderExtensions
{
    public static IQueryable<T> Filter<T>(this IQueryable<T> query, Expression<Func<T, bool>> filterExpression)
    {
        return query.Where(filterExpression);
    }

    public static IQueryable<T> Sort<T, TKey>(this IQueryable<T> query, Expression<Func<T, TKey>> keySelector, bool ascending = true)
    {
        if (ascending)
            return query.OrderBy(keySelector);
        else
            return query.OrderByDescending(keySelector);
    }

    public static IQueryable<T> Page<T>(this IQueryable<T> query, int pageNumber, int pageSize)
    {
        return query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    }

    public static IQueryable<TResult> Project<T, TResult>(this IQueryable<T> query, Expression<Func<T, TResult>> selector)
    {
        return query.Select(selector);
    }
}
