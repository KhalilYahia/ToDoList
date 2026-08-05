using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OpsManager.Domain.Common;
using OpsManager.Domain.Repositories;
using OpsManager.Repository.Persistence;

namespace OpsManager.Repository.Repositories;

public sealed class UnitOfWork(OpsManagerDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
        Type entityType = typeof(TEntity);
        if (!_repositories.TryGetValue(entityType, out object? repository))
        {
            repository = new GenericRepository<TEntity>(context);
            _repositories.Add(entityType, repository);
        }

        return (IGenericRepository<TEntity>)repository;
    }

    public async Task ExecuteWithStrategyAsync(Func<Task> operation)
    {
        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(operation);
    }
  
    public async Task<TResult> ExecuteWithStrategyAsync<TResult>(Func<Task<TResult>> operation)
    {
        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(operation);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new UnitOfWorkTransaction(transaction);
    }

    private sealed class UnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
