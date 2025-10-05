using System.Reflection;
using Common.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.SeedWork;

public class GenericRepository<T> : IGenericRepository<T> where T : Entity
{
    public readonly ApplicationDbContext context;

    public GenericRepository(ApplicationDbContext context)
    {
        this.context = context;
    }

    public T? GetById(int id)
    {
        return context.Set<T>().Find(id);
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await context.Set<T>().FindAsync(id);
    }

    public List<T> GetAll()
    {
        return context.Set<T>().ToList();
    }

    public async Task<List<T>> GetAllAsync()
    {
        return await context.Set<T>().ToListAsync();
    }

    public void Add(T entity)
    {
        context.Set<T>().Add(entity);
    }

    public void AddRange(List<T> entities)
    {
        context.Set<T>().AddRange(entities);
    }

    public void Update(T entity)
    {
        // Auto set DateTime.UtcNow on UpdatedOnUtc when update Entity
        PropertyInfo? prop = entity.GetType().GetProperty("UpdatedOnUtc");
        if (prop != null && prop.PropertyType == typeof(DateTime?))
        {
            prop.SetValue(entity, DateTime.UtcNow);
        }

        // EF Core is already tracking the existing entity
    }

    public void UpdateRange(List<T> entities)
    {
        // Auto set DateTime.UtcNow on UpdatedOnUtc when update Entities
        foreach (var entity in entities)
        {
            PropertyInfo? prop = entity.GetType().GetProperty("UpdatedOnUtc");
            if (prop != null && prop.PropertyType == typeof(DateTime?))
            {
                prop.SetValue(entity, DateTime.UtcNow);
            }
        }

        // EF Core is already tracking the existing entities
    }

    public void Delete(T entity)
    {
        context.Set<T>().Remove(entity);
    }

    public void DeleteRange(List<T> entities)
    {
        context.Set<T>().RemoveRange(entities);
    }

    public IQueryable<T> GetQueryable()
    {
        return context.Set<T>().AsQueryable();
    }
}
