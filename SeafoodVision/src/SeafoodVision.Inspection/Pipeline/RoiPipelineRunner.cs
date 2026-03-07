using OpenCvSharp;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Inspection.Pipeline.Models;

namespace SeafoodVision.Inspection.Pipeline;

/// <summary>
/// Executes the sequential chain of <see cref="InspectionStep"/>s for a single <see cref="RoiDefinition"/>.
/// Manages cropping the RegionOfInterest from the full frame and passing 
/// intermediate Mats between steps safely.
/// </summary>
public sealed class RoiPipelineRunner
{
    /// <summary>
    /// Runs the pipeline for a given ROI up to an optional step limit (useful for UI previews).
    /// </summary>
    /// <param name="fullFrame">The complete reference BGR camera frame.</param>
    /// <param name="roi">The ROI definition and its steps.</param>
    /// <param name="stepLimit">Maximum number of steps to execute.</param>
    /// <returns>A new <see cref="RoiResult"/> containing the final processed Mat and pass/fail state.</returns>
    public RoiResult Run(Mat fullFrame, RoiDefinition roi, int stepLimit = int.MaxValue)
    {
        // 1. Convert normalised coordinates to pixel coordinates on this frame
        var rect = roi.Region.ToPixelRect(fullFrame.Width, fullFrame.Height);

        // Optional safety clamp if ROI bounds go out of the frame
        var safeRect = new OpenCvSharp.Rect(
            Math.Max(0, rect.X),
            Math.Max(0, rect.Y),
            Math.Min(rect.Width, fullFrame.Width - rect.X),
            Math.Min(rect.Height, fullFrame.Height - rect.Y)
        );

        if (safeRect.Width <= 0 || safeRect.Height <= 0)
            return new RoiResult(roi, new Mat(), false);

        // 2. Crop out the ROI to start the pipeline
        Mat initialMat = new Mat(fullFrame, safeRect).Clone();
        Mat currentMat = initialMat;
        bool isPassed = true;

        var orderedSteps = roi.Steps.OrderBy(s => s.Order).Take(stepLimit).ToList();

        // Keep a snapshot of every step's output so that any later step can reference it
        // Key = 1-based step Order, Value = Mat (owned by this dictionary until Run returns)
        var stepOutputs = new Dictionary<int, Mat>();

        // 3. Thread the Mat through each step
        for (int i = 0; i < orderedSteps.Count; i++)
        {
            var step = orderedSteps[i];

            // Determine the primary input for this step
            Mat primaryInput;
            if (step.InputStepOrder.HasValue && stepOutputs.TryGetValue(step.InputStepOrder.Value, out var overrideInput))
            {
                primaryInput = overrideInput;
            }
            else
            {
                primaryInput = currentMat;
            }

            // Determine secondary input (used by SubtractImage, IntersectionRegion)
            Mat? secondaryInput = GetSecondaryInput(step, stepOutputs);

            var nextMat = StepExecutor.Execute(primaryInput, secondaryInput, step);

            // Store this step's output for potential future reference
            stepOutputs[step.Order] = nextMat;

            // Advance the "current" pointer (sequential chain still works as before)
            currentMat = nextMat;

            // Simple heuristic for now: if a defect detector or template match returns an empty mask, it "failed".
            if (currentMat.Empty())
            {
                isPassed = false;
                break;
            }
        }

        // Clone the final output into an independent Mat for the caller
        var resultMat = currentMat.Clone();

        // Dispose the initial ROI crop and all intermediate step outputs
        initialMat.Dispose();
        foreach (var kv in stepOutputs)
            kv.Value.Dispose();

        return new RoiResult(roi, resultMat, isPassed);
    }

    /// <summary>
    /// Resolves the secondary input Mat for dual-input steps.
    /// Returns <c>null</c> if the step does not require a secondary input or the reference is unavailable.
    /// </summary>
    private static Mat? GetSecondaryInput(
        SeafoodVision.Domain.Entities.InspectionStep step,
        Dictionary<int, Mat> stepOutputs)
    {
        // Deserialise only the fields we need (SecondaryInputStepOrder)
        int? secondaryOrder = step.StepType switch
        {
            SeafoodVision.Domain.Enums.StepType.SubtractImage =>
                TryGetSecondaryOrder<SeafoodVision.Inspection.Pipeline.Models.SubtractImageParams>(step.ParametersJson),
            SeafoodVision.Domain.Enums.StepType.IntersectionRegion =>
                TryGetSecondaryOrder<SeafoodVision.Inspection.Pipeline.Models.IntersectionRegionParams>(step.ParametersJson),
            _ => null
        };

        if (secondaryOrder.HasValue && stepOutputs.TryGetValue(secondaryOrder.Value, out var secondary))
            return secondary;

        return null;
    }

    private static int? TryGetSecondaryOrder<T>(string json) where T : IHasSecondaryInput, new()
    {
        try
        {
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var p = System.Text.Json.JsonSerializer.Deserialize<T>(json, opts);
            return p?.SecondaryInputStepOrder;
        }
        catch { return null; }
    }
}
