using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Inspection.Cache;
using SeafoodVision.Inspection.Pipeline;

namespace SeafoodVision.Inspection.Services;

/// <summary>
/// Options for <see cref="InspectionService"/>.
/// Bound from the "Inspection" section of appsettings.json.
/// </summary>
public sealed class InspectionOptions
{
    public const string SectionName = "Inspection";

    /// <summary>Camera ID whose active recipe this service will use.</summary>
    public string CameraId { get; set; } = "CAM-01";
}

/// <summary>
/// Applies the active teaching recipe to a set of raw detections.
/// If an active <see cref="InspectionRecipe"/> exists for the configured camera, each
/// detection whose centre falls inside a <em>failed</em> ROI pipeline is removed.
/// If no recipe is active, all detections pass through unchanged.
/// </summary>
public sealed class InspectionService : IInspectionService
{
    private readonly IRecipeCache _recipeCache;
    private readonly RecipePipelineRunner _pipelineRunner;
    private readonly string _cameraId;
    private readonly ILogger<InspectionService> _logger;

    public InspectionService(
        IRecipeCache recipeCache,
        RecipePipelineRunner pipelineRunner,
        IOptions<InspectionOptions> options,
        ILogger<InspectionService> logger)
    {
        _recipeCache = recipeCache;
        _pipelineRunner = pipelineRunner;
        _cameraId = options.Value.CameraId;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SeafoodItem>> InspectAsync(
        byte[] frameData,
        IReadOnlyList<SeafoodItem> detections,
        CancellationToken cancellationToken = default)
    {
        // Retrieve the cached active recipe (no DB hit on subsequent frames)
        var recipe = await _recipeCache.GetActiveAsync(_cameraId, cancellationToken);

        if (recipe is null)
        {
            // No active recipe configured → pass all detections through unchanged
            _logger.LogDebug("No active recipe for camera {CameraId}; skipping inspection.", _cameraId);
            return detections;
        }

        if (recipe.RoiDefinitions.Count == 0)
        {
            _logger.LogDebug("Recipe '{Name}' for camera {CameraId} has no ROI definitions; skipping inspection.", recipe.Name, _cameraId);
            return detections;
        }

        // Decode the frame for ROI pipeline processing
        using var bgr = Cv2.ImDecode(frameData, ImreadModes.Color);
        if (bgr.Empty())
        {
            _logger.LogWarning("Inspection skipped: failed to decode frame for camera {CameraId}.", _cameraId);
            return detections;
        }

        // Run all ROI pipelines (sequential or parallel per recipe config)
        var roiResults = await _pipelineRunner.RunAsync(bgr, recipe, cancellationToken);

        try
        {
            // Collect ROIs that did NOT pass their vision chain
            var failedRois = roiResults.Where(r => !r.IsPassed).ToList();

            if (failedRois.Count == 0)
                return detections;

            // Remove any detection whose centre-point falls within a failed ROI
            var filtered = detections
                .Where(item => !failedRois.Any(r =>
                    r.Roi.Region.Contains(
                        item.BoundingBox.CenterX,
                        item.BoundingBox.CenterY)))
                .ToList()
                .AsReadOnly();

            _logger.LogDebug(
                "Inspection filtered {Removed} of {Total} detections (camera {CameraId}, {FailedRois} failed ROI(s)).",
                detections.Count - filtered.Count,
                detections.Count,
                _cameraId,
                failedRois.Count);

            return filtered;
        }
        finally
        {
            // Dispose unmanaged Mat objects produced by the pipeline
            foreach (var r in roiResults)
                r.Dispose();
        }
    }
}


