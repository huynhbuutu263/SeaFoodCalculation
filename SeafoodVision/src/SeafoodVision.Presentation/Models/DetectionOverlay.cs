namespace SeafoodVision.Presentation.Models;

/// <summary>
/// UI model representing a single detected item overlay on the video frame.
/// Coordinates are normalized (0–1) relative to the frame width/height.
/// </summary>
public sealed class DetectionOverlay
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public float Confidence { get; init; }
    public string Label { get; init; } = string.Empty;
}

