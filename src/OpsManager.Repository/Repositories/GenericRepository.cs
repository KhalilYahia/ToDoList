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

    public Task<PagedResult<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate,
        PageRequest page,
        CancellationToken cancellationToken = default) =>
        ListAsync(predicate, page, null, cancellationToken);

    public async Task<PagedResult<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate,
        PageRequest page,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
        CancellationToken cancellationToken = default)
    {
        page.Validate();
        IQueryable<TEntity> query = _entities.AsNoTracking();
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        if (orderBy is not null)
        {
            query = orderBy(query);
        }
        else
        {
            query = query.OrderBy(entity => entity.Id);
        }

        IReadOnlyList<TEntity> items = await query
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TEntity>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<TResult>> ProjectAsync<TResult>(
        Expression<Func<TEntity, bool>>? predicate,
        Expression<Func<TEntity, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = _entities.AsNoTracking();
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query.Select(selector).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TResult>> ProjectJoinAsync<TOther, TKey, TResult>(
        Expression<Func<TEntity, bool>>? predicate,
        Expression<Func<TOther, bool>>? otherPredicate,
        Expression<Func<TEntity, TKey>> keySelector,
        Expression<Func<TOther, TKey>> otherKeySelector,
        Expression<Func<TEntity, TOther, TResult>> selector,
        CancellationToken cancellationToken = default)
        where TOther : BaseEntity
    {
        IQueryable<TEntity> left = _entities.AsNoTracking();
        IQueryable<TOther> right = context.Set<TOther>().AsNoTracking();
        if (predicate is not null)
        {
            left = left.Where(predicate);
        }

        if (otherPredicate is not null)
        {
            right = right.Where(otherPredicate);
        }

        return await left
            .Join(right, keySelector, otherKeySelector, selector)
            .ToListAsync(cancellationToken);
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

    public void DeletePermanently(TEntity entity) => _entities.Remove(entity);
}
