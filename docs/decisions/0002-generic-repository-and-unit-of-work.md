# ADR 0002: Generic repository and UnitOfWork

- Status: Accepted
- Date: 2026-07-22

## Decision

Application workflows access persistence through `IUnitOfWork` and `IGenericRepository<TEntity>` contracts owned by Domain. The repository exposes materialized single values, paged lists, counts, existence checks, mutations, one logical save, and a transaction abstraction. It never returns `IQueryable`, `DbSet`, `DbContext`, or EF transaction types.

Repository reads use `AsNoTracking`; paged reads use a stable ID order and enforce a maximum page size of 200. A scoped UnitOfWork caches one generic repository instance per entity type. Default removal works only for `ISoftDeletable`; hard deletion must be deliberate infrastructure code.

## Consequences

- Service workflows remain persistence-technology independent.
- One logical workflow can commit once and can use an atomic transaction without referencing EF Core.
- Query shapes more complex than the initial expression filters should add cohesive repository/specification contracts rather than leaking `IQueryable`.
