using Microsoft.EntityFrameworkCore;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Infrastructure.Data;

namespace SeafoodVision.Infrastructure.Data.Repositories;

/// <inheritdoc cref="ISessionRepository"/>
public sealed class SessionRepository : ISessionRepository
{
    private readonly SeafoodDbContext _context;

    public SessionRepository(SeafoodDbContext context) => _context = context;

    public async Task<CountingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.CountingSessions.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<CountingSession>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.CountingSessions.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CountingSession>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        => await _context.CountingSessions
            .AsNoTracking()
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CountingSession entity, CancellationToken cancellationToken = default)
        => await _context.CountingSessions.AddAsync(entity, cancellationToken);

    public Task UpdateAsync(CountingSession entity, CancellationToken cancellationToken = default)
    {
        _context.CountingSessions.Update(entity);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
