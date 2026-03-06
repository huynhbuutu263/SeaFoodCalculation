using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Domain.Interfaces;

/// <summary>
/// Repository contract for <see cref="InspectionRecipe"/> aggregate roots.
/// </summary>
public interface IRecipeRepository : IRepository<InspectionRecipe>
{
    /// <summary>
    /// Returns all active recipes for the given camera, including their ROI definitions and steps,
    /// ordered by ROI order then step order.
    /// </summary>
    Task<IReadOnlyList<InspectionRecipe>> GetActiveAsync(
        string cameraId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all recipes for the given camera (active and inactive),
    /// for displaying in the recipe editor list.
    /// </summary>
    Task<IReadOnlyList<InspectionRecipe>> GetByCameraAsync(
        string cameraId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a recipe and all its owned ROIs and steps.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
