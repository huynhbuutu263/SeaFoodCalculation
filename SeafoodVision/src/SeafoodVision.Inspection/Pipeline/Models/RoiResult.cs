using OpenCvSharp;
using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Inspection.Pipeline.Models;

/// <summary>
/// Holds the result of running an <see cref="RoiDefinition"/>'s pipeline.
/// </summary>
public sealed record RoiResult(RoiDefinition Roi, Mat ResultMat, bool IsPassed) : IDisposable
{
    public void Dispose()
    {
        ResultMat?.Dispose();
    }
}
