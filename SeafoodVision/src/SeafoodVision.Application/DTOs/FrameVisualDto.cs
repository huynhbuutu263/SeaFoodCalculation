using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Application.DTOs;

/// <summary>
/// Lightweight snapshot of a frame and its tracked items for UI display.
/// </summary>
public sealed class FrameVisualDto
{
    public required byte[] FrameBytes { get; init; }
    public required IReadOnlyList<SeafoodItem> Items { get; init; }
    public required DateTime CapturedAt { get; init; }
    public required int TotalCount { get; init; }
}

