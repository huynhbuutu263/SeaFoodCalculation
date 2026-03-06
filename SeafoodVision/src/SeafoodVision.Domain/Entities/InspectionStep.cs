using SeafoodVision.Domain.Enums;
using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Domain.Entities;

/// <summary>
/// An individual OpenCV processing step inside a <see cref="RoiDefinition"/>'s vision flow.
/// Parameters are stored as a JSON string so that new step types can be added
/// without requiring a database schema migration.
/// </summary>
public sealed class InspectionStep
{
    public Guid Id { get; private set; }

    /// <summary>1-based execution order within the parent <see cref="RoiDefinition"/>.</summary>
    public int Order { get; private set; }

    /// <summary>The OpenCV operation this step performs.</summary>
    public StepType StepType { get; private set; }

    /// <summary>
    /// JSON object that carries all parameters for this step type.
    /// Example: <c>{"ThreshValue":128,"MaxValue":255,"ThreshType":"Otsu"}</c>
    /// </summary>
    public string ParametersJson { get; private set; }

    // Navigation (set by EF Core)
    public Guid RoiDefinitionId { get; private set; }

    // EF Core constructor
    private InspectionStep()
    {
        ParametersJson = "{}";
    }

    public static InspectionStep Create(int order, StepType stepType, string parametersJson = "{}")
    {
        if (order < 1) throw new ArgumentOutOfRangeException(nameof(order), "Order must be ≥ 1.");
        ArgumentException.ThrowIfNullOrWhiteSpace(parametersJson);

        return new InspectionStep
        {
            Id = Guid.NewGuid(),
            Order = order,
            StepType = stepType,
            ParametersJson = parametersJson
        };
    }

    public void UpdateOrder(int newOrder)
    {
        if (newOrder < 1) throw new ArgumentOutOfRangeException(nameof(newOrder));
        Order = newOrder;
    }

    public void UpdateParameters(string parametersJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parametersJson);
        ParametersJson = parametersJson;
    }
}
