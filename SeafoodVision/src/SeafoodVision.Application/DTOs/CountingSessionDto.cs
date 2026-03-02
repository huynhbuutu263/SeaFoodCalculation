namespace SeafoodVision.Application.DTOs;

/// <summary>
/// Summary DTO returned to the UI layer for a completed or active counting session.
/// </summary>
public sealed record CountingSessionDto(
    Guid Id,
    string CameraId,
    DateTime StartedAt,
    DateTime? EndedAt,
    int TotalCount);
