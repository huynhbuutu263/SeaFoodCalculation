namespace SeafoodVision.Domain.Entities;

/// <summary>
/// Represents a video frame and all detections produced by the inference model for that frame.
/// </summary>
public sealed class DetectionFrame
{
    public long FrameIndex { get; }
    public DateTime CapturedAt { get; }
    public IReadOnlyList<SeafoodItem> Detections { get; }

    public DetectionFrame(long frameIndex, DateTime capturedAt, IEnumerable<SeafoodItem> detections)
    {
        FrameIndex = frameIndex;
        CapturedAt = capturedAt;
        Detections = detections.ToList().AsReadOnly();
    }
}
