using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Common.SeedWork;

public interface IUnitOfWork
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : Entity;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> BulkSaveChangesAsync();
    DbContext GetDbContext();
    Task ExecuteAsync(string query, Dictionary<string, object?>? parameters = null, CommandType commandType = CommandType.StoredProcedure, int? commandTimeout = null);
    Task<List<T>> QueryAsync<T>(string query, Dictionary<string, object?>? parameters = null, CommandType commandType = CommandType.StoredProcedure, int? commandTimeout = null);

    #region Transaction
    Task<IDbContextTransaction?> BeginTransactionAsync();
    Task CommitTransactionAsync(IDbContextTransaction? transaction);
    Task CommitTransactionBulkOperationsAsync(IDbContextTransaction? transaction);
    void RollbackTransaction();
    #endregion

    #region Bulk Operations
    Task<int> BulkInsertAsync<T>(List<T> entities) where T : Entity;
    Task<int> BulkUpdateAsync<T>(List<T> entities) where T : Entity;
    Task<int> BulkDeleteAsync<T>(List<T> entities) where T : Entity;
    #endregion
}