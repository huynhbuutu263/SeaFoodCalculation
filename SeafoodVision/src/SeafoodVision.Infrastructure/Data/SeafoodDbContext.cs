using Microsoft.EntityFrameworkCore;
using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Infrastructure.Data;

/// <summary>
/// EF Core database context for the SeafoodVision MySQL database.
/// </summary>
public sealed class SeafoodDbContext : DbContext
{
    public SeafoodDbContext(DbContextOptions<SeafoodDbContext> options) : base(options) { }

    public DbSet<CountingSession> CountingSessions => Set<CountingSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CountingSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.CameraId).HasMaxLength(128).IsRequired();
            e.Property(s => s.TotalCount).IsRequired();
            e.Property(s => s.StartedAt).IsRequired();
            e.HasIndex(s => s.StartedAt);
        });
    }
}
