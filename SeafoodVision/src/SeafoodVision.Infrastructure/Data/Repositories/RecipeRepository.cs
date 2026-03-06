using Microsoft.EntityFrameworkCore;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Domain.ValueObjects;
using SeafoodVision.Infrastructure.Data;
using System.Drawing;
using System.Text.Json;

namespace SeafoodVision.Infrastructure.Data.Repositories;

/// <inheritdoc cref="IRecipeRepository"/>
public sealed class RecipeRepository : IRecipeRepository
{
    private readonly SeafoodDbContext _context;

    public RecipeRepository(SeafoodDbContext context) => _context = context;

    // Replace the problematic code with the following:

    public async Task<InspectionRecipe?> GetByIdAsync(Guid id, CancellationToken ct = default)
       => await _context.InspectionRecipes
           .Include(r => r.RoiDefinitions) // Use strongly-typed navigation properties
               .ThenInclude(roi => roi.Steps) // Ensure Steps is a valid navigation property
           .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<InspectionRecipe>> GetAllAsync(CancellationToken ct = default)
       => await _context.InspectionRecipes
            .Include(r => r.RoiDefinitions)
                .ThenInclude(roi => roi.Steps)
            .AsNoTracking()
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(InspectionRecipe entity, CancellationToken ct = default)
    {
        await PopulateShadowPropertiesAsync(entity);
        await _context.InspectionRecipes.AddAsync(entity, ct);
    }

    public Task UpdateAsync(InspectionRecipe entity, CancellationToken ct = default)
    {
        PopulateShadowPropertiesAsync(entity).GetAwaiter().GetResult();
        _context.InspectionRecipes.Update(entity);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    // ── IRecipeRepository ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<InspectionRecipe>> GetActiveAsync(
   string cameraId, CancellationToken ct = default)
   => await _context.InspectionRecipes
            .Include(r => r.RoiDefinitions)
                .ThenInclude(roi => roi.Steps)
            .AsNoTracking()
            .Where(r => r.CameraId == cameraId && r.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<InspectionRecipe>> GetByCameraAsync(
       string cameraId, CancellationToken ct = default)
       => await _context.InspectionRecipes
            .Include(r => r.RoiDefinitions)
                .ThenInclude(roi => roi.Steps)
            .AsNoTracking()
            .Where(r => r.CameraId == cameraId)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var recipe = await _context.InspectionRecipes.FindAsync([id], ct);
        if (recipe is not null)
            _context.InspectionRecipes.Remove(recipe);
    }

    // ── Shadow property helpers (RegionOfInterest serialisation) ──────────────

    /// <summary>
    /// Writes the <see cref="RegionOfInterest"/> value object from each ROI into the
    /// EF shadow properties (<c>region_type</c> and <c>points_json</c>) before
    /// the entity is tracked or saved.
    /// </summary>
    private Task PopulateShadowPropertiesAsync(InspectionRecipe recipe)
    {
        foreach (var roi in recipe.RoiDefinitions)
        {
            var entry = _context.Entry(roi);
            entry.Property("RegionTypeRaw").CurrentValue = (byte)roi.Region.RegionType;
            entry.Property("PointsJson").CurrentValue = SerialisePoints(roi.Region.Points);
        }
        return Task.CompletedTask;
    }

    private static string SerialisePoints(IReadOnlyList<PointF> points)
        => JsonSerializer.Serialize(points.Select(p => new { x = p.X, y = p.Y }));
}
