using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Inspection.Cache;

/// <summary>
/// Provides a cached view of the active <see cref="InspectionRecipe"/> for a given camera.
/// Caches the active recipe in memory to avoid a database round-trip on every inspected frame.
/// The cache entry must be explicitly invalidated when a recipe is saved via the editor.
/// </summary>
public interface IRecipeCache
{
    /// <summary>
    /// Returns the most-recently-updated active recipe for the given camera,
    /// fetching from the database only on a cache miss.
    /// Returns <c>null</c> if no active recipe exists for that camera (pass-through mode).
    /// </summary>
    Task<InspectionRecipe?> GetActiveAsync(string cameraId, CancellationToken ct = default);

    /// <summary>
    /// Removes the cached recipe entry for the specified camera so that
    /// the next call to <see cref="GetActiveAsync"/> re-reads from the database.
    /// Call this immediately after saving a recipe in the Recipe Editor.
    /// </summary>
    void Invalidate(string cameraId);
}
