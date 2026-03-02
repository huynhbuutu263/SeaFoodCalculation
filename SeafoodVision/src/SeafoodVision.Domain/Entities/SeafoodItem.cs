using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Domain.Entities;

/// <summary>
/// Represents a single seafood item detected and tracked in a video stream.
/// </summary>
public sealed class SeafoodItem
{
    public TrackingId TrackingId { get; }
    public string Label { get; }
    public float Confidence { get; }
    public BoundingBox BoundingBox { get; private set; }
    public DateTime FirstSeenAt { get; }
    public DateTime LastSeenAt { get; private set; }
    public bool IsCounted { get; private set; }

    public SeafoodItem(TrackingId trackingId, string label, float confidence, BoundingBox boundingBox, DateTime detectedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        TrackingId = trackingId;
        Label = label;
        Confidence = confidence;
        BoundingBox = boundingBox;
        FirstSeenAt = detectedAt;
        LastSeenAt = detectedAt;
    }

    public void UpdatePosition(BoundingBox newBox, DateTime seenAt)
    {
        BoundingBox = newBox;
        LastSeenAt = seenAt;
    }

    public void MarkAsCounted() => IsCounted = true;
}
