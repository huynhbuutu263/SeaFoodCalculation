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
        var currentMat = new Mat(fullFrame, safeRect).Clone();
        bool isPassed = true;

        var orderedSteps = roi.Steps.OrderBy(s => s.Order).Take(stepLimit).ToList();
        
        // 3. Thread the Mat through each step
        foreach (var step in orderedSteps)
        {
            var nextMat = StepExecutor.Execute(currentMat, step);
            
            // Dispose the old intermediate Mat to prevent unmanaged memory leaks
            currentMat.Dispose(); 
            currentMat = nextMat;

            // Simple heuristic for now: if a defect detector or template match returns an empty mask, it "failed".
            // More robust pass/fail expression logic can be added later.
            if (currentMat.Empty()) 
            {
                isPassed = false;
                break;
            }
        }

        return new RoiResult(roi, currentMat, isPassed);
    }
}
