using System.Linq.Expressions;
using OpsManager.Domain.Common;

namespace OpsManager.Domain.Repositories;

public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaximumPageSize = 200;

    public int Skip => (Page - 1) * PageSize;

    public PageRequest Validate()
    {
        if (Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Page), "Page must be at least one.");
        }

        if (PageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(PageSize), $"Page size must be between 1 and {MaximumPageSize}.");
        }

        return this;
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public interface IGenericRepository<TEntity>
    where TEntity : BaseEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate,
        PageRequest page,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface ITenantContext
{
    Guid? OrganizationId { get; }

    bool BypassTenantFilter { get; }
}
