namespace SeafoodVision.Domain.Enums;

/// <summary>
/// Controls whether ROI pipelines in a recipe run in parallel or sequentially.
/// </summary>
public enum ExecutionMode : byte
{
    /// <summary>Each ROI pipeline runs one after another (lowest resource usage).</summary>
    Sequential = 0,

    /// <summary>All ROI pipelines run concurrently via Task.WhenAll (faster, more CPU).</summary>
    Parallel = 1
}
