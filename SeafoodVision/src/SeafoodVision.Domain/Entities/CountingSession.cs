namespace SeafoodVision.Domain.Entities;

/// <summary>
/// Persisted record of a complete counting run.
/// </summary>
public sealed class CountingSession
{
    public Guid Id { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public int TotalCount { get; private set; }
    public string CameraId { get; private set; }

    private CountingSession() { CameraId = string.Empty; } // EF Core

    public static CountingSession Start(string cameraId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
        return new CountingSession
        {
            Id = Guid.NewGuid(),
            CameraId = cameraId,
            StartedAt = DateTime.UtcNow,
            TotalCount = 0
        };
    }

    public void IncrementCount(int delta = 1)
    {
        if (delta < 1) throw new ArgumentOutOfRangeException(nameof(delta));
        TotalCount += delta;
    }

    public void End()
    {
        if (EndedAt.HasValue) throw new InvalidOperationException("Session already ended.");
        EndedAt = DateTime.UtcNow;
    }
}
