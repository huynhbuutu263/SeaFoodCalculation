using SeafoodVision.Domain.Enums;
using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Domain.Entities;

/// <summary>
/// A named region of interest within an <see cref="InspectionRecipe"/>.
/// Each ROI has its own ordered chain of <see cref="InspectionStep"/>s that form
/// an independent vision flow.
/// </summary>
public sealed class RoiDefinition
{
    private readonly List<InspectionStep> _steps = [];

    public Guid Id { get; private set; }

    /// <summary>Display name for this ROI (e.g. "Head Zone", "Body Zone").</summary>
    public string Name { get; private set; }

    /// <summary>1-based execution order within the parent <see cref="InspectionRecipe"/>.</summary>
    public int Order { get; private set; }

    /// <summary>Shape and normalised coordinates of this ROI.</summary>
    public RegionOfInterest Region { get; private set; }

    /// <summary>Ordered vision steps that are executed for this ROI.</summary>
    public IReadOnlyList<InspectionStep> Steps => _steps.AsReadOnly();

    // Navigation (set by EF Core)
    public Guid RecipeId { get; private set; }

    // EF Core constructor
    private RoiDefinition()
    {
        Name = string.Empty;
        Region = RegionOfInterest.FromRectangle(new(0f, 0f), new(1f, 1f));
    }

    public static RoiDefinition Create(string name, int order, RegionOfInterest region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(region);
        if (order < 1) throw new ArgumentOutOfRangeException(nameof(order));

        return new RoiDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Order = order,
            Region = region
        };
    }

    // ── Mutation methods ──────────────────────────────────────────────────────

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void UpdateRegion(RegionOfInterest region)
    {
        ArgumentNullException.ThrowIfNull(region);
        Region = region;
    }

    public void UpdateOrder(int order)
    {
        if (order < 1) throw new ArgumentOutOfRangeException(nameof(order));
        Order = order;
    }

    // ── Step management ───────────────────────────────────────────────────────

    public InspectionStep AddStep(StepType stepType, string parametersJson = "{}")
    {
        int nextOrder = _steps.Count == 0 ? 1 : _steps.Max(s => s.Order) + 1;
        var step = InspectionStep.Create(nextOrder, stepType, parametersJson);
        _steps.Add(step);
        return step;
    }

    public void RemoveStep(Guid stepId)
    {
        var step = _steps.Find(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step {stepId} not found in ROI '{Name}'.");
        _steps.Remove(step);
        ReorderSteps();
    }

    public void MoveStepUp(Guid stepId)    => SwapStep(stepId, -1);
    public void MoveStepDown(Guid stepId)  => SwapStep(stepId, +1);

    private void SwapStep(Guid stepId, int direction)
    {
        var ordered = _steps.OrderBy(s => s.Order).ToList();
        int idx = ordered.FindIndex(s => s.Id == stepId);
        int target = idx + direction;
        if (idx < 0 || target < 0 || target >= ordered.Count) return;

        int tmpOrder = ordered[idx].Order;
        ordered[idx].UpdateOrder(ordered[target].Order);
        ordered[target].UpdateOrder(tmpOrder);
    }

    private void ReorderSteps()
    {
        int i = 1;
        foreach (var s in _steps.OrderBy(s => s.Order))
            s.UpdateOrder(i++);
    }
}
