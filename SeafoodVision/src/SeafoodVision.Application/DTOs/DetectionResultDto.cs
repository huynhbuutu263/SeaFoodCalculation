using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Application.DTOs;

/// <summary>
/// Represents a single detection returned by the Python inference API.
/// </summary>
public sealed record DetectionResultDto(
    string Label,
    float Confidence,
    float X,
    float Y,
    float Width,
    float Height)
{
    public BoundingBox ToBoundingBox() => new(X, Y, Width, Height);
}
