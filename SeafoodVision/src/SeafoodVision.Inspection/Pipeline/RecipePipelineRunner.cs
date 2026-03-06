using OpenCvSharp;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Inspection.Pipeline.Models;

namespace SeafoodVision.Inspection.Pipeline;

/// <summary>
/// Orchestrates the execution of a complete <see cref="InspectionRecipe"/>.
/// Depending on <see cref="ExecutionMode"/>, delegates ROI pipelines to <see cref="RoiPipelineRunner"/>
/// either sequentially or in parallel via the ThreadPool.
/// </summary>
public sealed class RecipePipelineRunner
{
    private readonly RoiPipelineRunner _roiRunner;

    public RecipePipelineRunner(RoiPipelineRunner roiRunner)
    {
        _roiRunner = roiRunner;
    }

    /// <summary>
    /// Runs all ROI pipelines defined in the recipe.
    /// Caller is responsible for disposing the results to release unmanaged Mats.
    /// </summary>
    public async Task<IReadOnlyList<RoiResult>> RunAsync(
        Mat fullFrame, 
        InspectionRecipe recipe, 
        CancellationToken ct = default)
    {
        if (recipe.RoiDefinitions.Count == 0)
            return [];

        if (recipe.ExecutionMode == ExecutionMode.Parallel)
        {
            // Offload CPU-bound OpenCV tasks to ThreadPool and run concurrently
            var tasks = recipe.RoiDefinitions.Select(roi => 
                Task.Run(() => 
                {
                    ct.ThrowIfCancellationRequested();
                    return _roiRunner.Run(fullFrame, roi);
                }, ct));
            
            return await Task.WhenAll(tasks);
        }
        else
        {
            // Run sequentially on the caller's thread (less resource intensive)
            var results = new List<RoiResult>(recipe.RoiDefinitions.Count);
            foreach (var roi in recipe.RoiDefinitions.OrderBy(r => r.Order))
            {
                ct.ThrowIfCancellationRequested();
                results.Add(_roiRunner.Run(fullFrame, roi));
            }
            return results;
        }
    }
}
