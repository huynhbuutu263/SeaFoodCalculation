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

    public async Task<InspectionRecipe?> GetByIdAsync(Guid id, CancellationToken ct = default)
       => await _context.InspectionRecipes
           .Include(r => r.RoiDefinitions)
               .ThenInclude(roi => roi.Steps)
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

    /// <summary>
    /// Updates an existing recipe and synchronises its ROI and Step children:
    /// new children are inserted, modified children are updated, and removed
    /// children are deleted (EF cascade handles grandchildren automatically).
    /// </summary>
    public async Task UpdateAsync(InspectionRecipe entity, CancellationToken ct = default)
    {
        await PopulateShadowPropertiesAsync(entity);

        // Load the currently-tracked version from the DB so we can diff
        var tracked = await _context.InspectionRecipes
            .Include(r => r.RoiDefinitions)
                .ThenInclude(roi => roi.Steps)
            .FirstOrDefaultAsync(r => r.Id == entity.Id, ct);

        if (tracked == null)
        {
            // Recipe was never persisted — treat as an Add
            await AddAsync(entity, ct);
            return;
        }

        // Update scalar properties on the root
        _context.Entry(tracked).CurrentValues.SetValues(entity);

        // ── Synchronise ROI collection ─────────────────────────────────────
        var incomingRoiIds = entity.RoiDefinitions.Select(r => r.Id).ToHashSet();

        // Delete ROIs that are no longer in the in-memory recipe
        foreach (var orphanRoi in tracked.RoiDefinitions
            .Where(r => !incomingRoiIds.Contains(r.Id))
            .ToList())
        {
            _context.RoiDefinitions.Remove(orphanRoi); // cascade removes its steps
        }

        foreach (var incomingRoi in entity.RoiDefinitions)
        {
            var trackedRoi = tracked.RoiDefinitions.FirstOrDefault(r => r.Id == incomingRoi.Id);

            if (trackedRoi == null)
            {
                // New ROI — set FK and add to context
                var newRoiEntry = _context.Entry(incomingRoi);
                newRoiEntry.Property("RegionTypeRaw").CurrentValue = (byte)incomingRoi.Region.RegionType;
                newRoiEntry.Property("PointsJson").CurrentValue = SerialisePoints(incomingRoi.Region.Points);
                _context.RoiDefinitions.Add(incomingRoi);
            }
            else
            {
                // Existing ROI — update scalar properties
                _context.Entry(trackedRoi).CurrentValues.SetValues(incomingRoi);
                var trackedEntry = _context.Entry(trackedRoi);
                trackedEntry.Property("RegionTypeRaw").CurrentValue = (byte)incomingRoi.Region.RegionType;
                trackedEntry.Property("PointsJson").CurrentValue = SerialisePoints(incomingRoi.Region.Points);

                // ── Synchronise Step collection for this ROI ───────────────
                var incomingStepIds = incomingRoi.Steps.Select(s => s.Id).ToHashSet();

                // Delete steps that were removed
                foreach (var orphanStep in trackedRoi.Steps
                    .Where(s => !incomingStepIds.Contains(s.Id))
                    .ToList())
                {
                    _context.InspectionSteps.Remove(orphanStep);
                }

                foreach (var incomingStep in incomingRoi.Steps)
                {
                    var trackedStep = trackedRoi.Steps.FirstOrDefault(s => s.Id == incomingStep.Id);
                    if (trackedStep == null)
                        _context.InspectionSteps.Add(incomingStep);
                    else
                        _context.Entry(trackedStep).CurrentValues.SetValues(incomingStep);
                }
            }
        }
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
