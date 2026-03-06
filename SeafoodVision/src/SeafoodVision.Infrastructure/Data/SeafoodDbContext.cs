using Microsoft.EntityFrameworkCore;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Infrastructure.Data.Configurations;

namespace SeafoodVision.Infrastructure.Data;

/// <summary>
/// EF Core database context for the SeafoodVision MySQL database.
/// </summary>
public sealed class SeafoodDbContext : DbContext
{
    public SeafoodDbContext(DbContextOptions<SeafoodDbContext> options) : base(options) { }

    // ── Existing tables ───────────────────────────────────────────────────────
    public DbSet<CountingSession> CountingSessions => Set<CountingSession>();

    // ── Recipe / teaching tables ──────────────────────────────────────────────
    public DbSet<InspectionRecipe>  InspectionRecipes  => Set<InspectionRecipe>();
    public DbSet<RoiDefinition>     RoiDefinitions     => Set<RoiDefinition>();
    public DbSet<InspectionStep>    InspectionSteps    => Set<InspectionStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── CountingSession ───────────────────────────────────────────────────
        modelBuilder.Entity<CountingSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.CameraId).HasMaxLength(128).IsRequired();
            e.Property(s => s.TotalCount).IsRequired();
            e.Property(s => s.StartedAt).IsRequired();
            e.HasIndex(s => s.StartedAt);
        });

        // ── Recipe / teaching entities ────────────────────────────────────────
        modelBuilder.ApplyConfiguration(new InspectionRecipeConfiguration());
        modelBuilder.ApplyConfiguration(new RoiDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new InspectionStepConfiguration());
    }
}
