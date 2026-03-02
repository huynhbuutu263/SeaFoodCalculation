using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Domain.Interfaces;

/// <summary>
/// Associates detections across frames using an object-tracking algorithm
/// (e.g. SORT / DeepSORT / ByteTrack) running entirely in C#.
/// </summary>
public interface ITrackingService
{
    /// <summary>
    /// Updates the tracker with new detections and returns the merged tracked items,
    /// including assigned TrackingIds and whether each item crossed the counting line.
    /// </summary>
    Task<IReadOnlyList<SeafoodItem>> UpdateAsync(
        IReadOnlyList<SeafoodItem> rawDetections,
        DateTime frameTimestamp,
        CancellationToken cancellationToken = default);
}
