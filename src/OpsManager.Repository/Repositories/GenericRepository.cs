using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OpsManager.Domain.Common;
using OpsManager.Domain.Repositories;
using OpsManager.Repository.Persistence;

namespace OpsManager.Repository.Repositories;

public sealed class GenericRepository<TEntity>(OpsManagerDbContext context) : IGenericRepository<TEntity>
    where TEntity : BaseEntity
{
    private readonly DbSet<TEntity> _entities = context.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _entities.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _entities.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);

    public async Task<PagedResult<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        page.Validate();
        IQueryable<TEntity> query = _entities.AsNoTracking();
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        IReadOnlyList<TEntity> items = await query
            .OrderBy(entity => entity.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TEntity>(items, page.Page, page.PageSize, totalCount);
    }

    public Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = _entities.AsNoTracking();
        return predicate is null ? query.CountAsync(cancellationToken) : query.CountAsync(predicate, cancellationToken);
    }

    public Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _entities.AsNoTracking().AnyAsync(predicate, cancellationToken);

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _entities.AddAsync(entity, cancellationToken).AsTask();

    public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        _entities.AddRangeAsync(entities, cancellationToken);

    public void Update(TEntity entity) => _entities.Update(entity);

    public void Remove(TEntity entity)
    {
        if (entity is not ISoftDeletable softDeletable)
        {
            throw new InvalidOperationException($"{typeof(TEntity).Name} does not support default deletion. Hard deletion requires explicit infrastructure code.");
        }

        softDeletable.DeletedAt = DateTimeOffset.UtcNow;
        _entities.Update(entity);
    }
}
