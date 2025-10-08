using System.Collections;
using System.Data;
using System.Diagnostics;
using Common.SeedWork;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using N.EntityFrameworkCore.Extensions;

namespace Identity.Infrastructure.SeedWork;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly ApplicationDbContext context;
    private readonly Hashtable repositories;

    #region Transaction
    private IDbContextTransaction? _currentTransaction;
    public IDbContextTransaction? GetCurrentTransaction() => _currentTransaction;
    public bool HasActiveTransaction => _currentTransaction != null;
    #endregion

    public UnitOfWork(ApplicationDbContext context)
    {
        this.context = context;
        repositories = new Hashtable();
    }

    #region Disposes the context and suppresses the finalizer for the current object.
    private bool _disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            context.Dispose();
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion

    [DebuggerStepThrough]
    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : Entity
    {
        // Find repository exists

        // 1. Get Name Type
        string type = typeof(TEntity).Name;

        // 2. Check exists
        if (!repositories.ContainsKey(type))
        {
            var repositoryType = typeof(GenericRepository<>);

            // Create instance repository
            var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(TEntity)), context);

            repositories.Add(type, repositoryInstance);
        }

        return (IGenericRepository<TEntity>)repositories[type]!;
    }

    public DbContext GetDbContext()
    {
        return context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> BulkSaveChangesAsync()
    {
        return await context.BulkSaveChangesAsync();
    }

    #region Dapper
    public async Task ExecuteAsync(string query, Dictionary<string, object?>? parameters = null, CommandType commandType = CommandType.StoredProcedure, int? commandTimeout = null)
    {
        await context.Database.GetDbConnection().ExecuteAsync(query, parameters, commandType: commandType, commandTimeout: commandTimeout);
    }

    public async Task<List<T>> QueryAsync<T>(string query, Dictionary<string, object?>? parameters = null, CommandType commandType = CommandType.StoredProcedure, int? commandTimeout = null)
    {
        var result = await context.Database.GetDbConnection().QueryAsync<T>(query, parameters, commandType: commandType, commandTimeout: commandTimeout);
        return result.ToList();
    }
    #endregion

    #region Transaction
    public async Task<IDbContextTransaction?> BeginTransactionAsync()
    {
        if (_currentTransaction != null) return null;

        _currentTransaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        return _currentTransaction;
    }

    public async Task CommitTransactionAsync(IDbContextTransaction? transaction)
    {
        if (transaction == null)
        {
            ArgumentNullException.ThrowIfNull(nameof(transaction));
            return;
        }
        if (transaction != _currentTransaction) throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

        try
        {
            await SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            RollbackTransaction();
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public async Task CommitTransactionBulkOperationsAsync(IDbContextTransaction? transaction)
    {
        if (transaction == null)
        {
            ArgumentNullException.ThrowIfNull(nameof(transaction));
            return;
        }
        if (transaction != _currentTransaction) throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

        try
        {
            await transaction.CommitAsync();
        }
        catch
        {
            RollbackTransaction();
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public void RollbackTransaction()
    {
        try
        {
            _currentTransaction?.Rollback();
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }
    #endregion

    #region Bulk Operations
    public async Task<int> BulkInsertAsync<T>(List<T> entities) where T : Entity
    {
        return await context.BulkInsertAsync(entities);
    }

    public async Task<int> BulkUpdateAsync<T>(List<T> entities) where T : Entity
    {
        return await context.BulkUpdateAsync(entities);
    }

    public async Task<int> BulkDeleteAsync<T>(List<T> entities) where T : Entity
    {
        return await context.BulkDeleteAsync(entities);
    }
    #endregion
}