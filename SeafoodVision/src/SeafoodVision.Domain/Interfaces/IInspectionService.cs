using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Domain.Interfaces;

public interface IInspectionService
{
    /// <summary>
    /// Applies rule-based or algorithmic inspection to the raw detections
    /// and returns the potentially filtered or adjusted list.
    /// </summary>
    /// <param name="frameData">Encoded frame bytes (for image-based inspection).</param>
    /// <param name="detections">Raw detections from the AI model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<SeafoodItem>> InspectAsync(
        byte[] frameData,
        IReadOnlyList<SeafoodItem> detections,
        CancellationToken cancellationToken = default);
}

