using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Domain.Interfaces;

/// <summary>
/// Generic repository contract for aggregate roots.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository specific to <see cref="CountingSession"/>.
/// </summary>
public interface ISessionRepository : IRepository<CountingSession>
{
    /// <summary>Returns the most recent sessions ordered by start time descending.</summary>
    Task<IReadOnlyList<CountingSession>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
