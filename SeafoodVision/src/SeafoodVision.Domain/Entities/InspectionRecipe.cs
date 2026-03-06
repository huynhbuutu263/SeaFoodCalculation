using SeafoodVision.Domain.Enums;
using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Domain.Entities;

/// <summary>
/// The top-level "parameter set" for an inspection run.
/// One recipe owns N <see cref="RoiDefinition"/>s; each ROI has its own vision step chain.
/// At runtime the recipe executes all its ROIs either sequentially or in parallel,
/// controlled by <see cref="ExecutionMode"/>.
/// </summary>
public sealed class InspectionRecipe
{
    private readonly List<RoiDefinition> _rois = [];

    public Guid Id { get; private set; }

    /// <summary>Human-readable name shown in the recipe list (e.g. "Shrimp Line 1").</summary>
    public string Name { get; private set; }

    /// <summary>Identifier of the camera this recipe applies to.</summary>
    public string CameraId { get; private set; }

    /// <summary>Whether this recipe is currently active for the pipeline.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Controls parallel vs sequential ROI execution.</summary>
    public ExecutionMode ExecutionMode { get; private set; }

    /// <summary>UTC timestamp when the recipe was created.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>UTC timestamp of the last modification.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Ordered ROI definitions belonging to this recipe.</summary>
    public IReadOnlyList<RoiDefinition> RoiDefinitions => _rois.AsReadOnly();

    // EF Core constructor
    private InspectionRecipe()
    {
        Name = string.Empty;
        CameraId = string.Empty;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static InspectionRecipe Create(
        string name,
        string cameraId,
        ExecutionMode executionMode = ExecutionMode.Sequential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);

        return new InspectionRecipe
        {
            Id = Guid.NewGuid(),
            Name = name,
            CameraId = cameraId,
            IsActive = false,
            ExecutionMode = executionMode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // ── Mutation methods ──────────────────────────────────────────────────────

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Touch();
    }

    public void SetExecutionMode(ExecutionMode mode) { ExecutionMode = mode; Touch(); }

    public void Activate()   { IsActive = true;  Touch(); }
    public void Deactivate() { IsActive = false; Touch(); }

    // ── ROI management ────────────────────────────────────────────────────────

    public RoiDefinition AddRoi(string name, RegionOfInterest region)
    {
        int nextOrder = _rois.Count == 0 ? 1 : _rois.Max(r => r.Order) + 1;
        var roi = RoiDefinition.Create(name, nextOrder, region);
        _rois.Add(roi);
        Touch();
        return roi;
    }

    public void RemoveRoi(Guid roiId)
    {
        var roi = _rois.Find(r => r.Id == roiId)
            ?? throw new InvalidOperationException($"ROI {roiId} not found in recipe '{Name}'.");
        _rois.Remove(roi);
        ReorderRois();
        Touch();
    }

    public void MoveRoiUp(Guid roiId)   => SwapRoi(roiId, -1);
    public void MoveRoiDown(Guid roiId) => SwapRoi(roiId, +1);

    private void SwapRoi(Guid roiId, int direction)
    {
        var ordered = _rois.OrderBy(r => r.Order).ToList();
        int idx = ordered.FindIndex(r => r.Id == roiId);
        int target = idx + direction;
        if (idx < 0 || target < 0 || target >= ordered.Count) return;

        int tmpOrder = ordered[idx].Order;
        ordered[idx].UpdateOrder(ordered[target].Order);
        ordered[target].UpdateOrder(tmpOrder);
        Touch();
    }

    private void ReorderRois()
    {
        int i = 1;
        foreach (var r in _rois.OrderBy(r => r.Order))
            r.UpdateOrder(i++);
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
