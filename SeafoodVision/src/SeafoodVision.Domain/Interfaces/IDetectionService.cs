using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Domain.Interfaces;

/// <summary>
/// Sends a raw frame to the AI inference backend and returns detections.
/// Implementations call the Python FastAPI service.
/// </summary>
public interface IDetectionService
{
    /// <summary>
    /// Performs object detection on the supplied frame bytes.
    /// </summary>
    /// <param name="frameData">JPEG/PNG encoded frame bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Zero or more detected items (untracked, raw detections).</returns>
    Task<IReadOnlyList<SeafoodItem>> DetectAsync(byte[] frameData, CancellationToken cancellationToken = default);
}
