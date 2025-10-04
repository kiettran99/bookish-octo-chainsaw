namespace Common.SeedWork;

public interface IGenericRepository<T> where T : IAggregateRoot
{
    T? GetById(int id);
    Task<T?> GetByIdAsync(int id);
    List<T> GetAll();
    Task<List<T>> GetAllAsync();
    void Add(T entity);
    void AddRange(List<T> entities);
    void Update(T entity);
    void UpdateRange(List<T> entities);
    void Delete(T entity);
    void DeleteRange(List<T> entities);
    IQueryable<T> GetQueryable();
}
