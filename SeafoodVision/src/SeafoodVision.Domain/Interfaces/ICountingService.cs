using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Domain.Interfaces;

/// <summary>
/// Maintains the running count of seafood items that have crossed the counting boundary.
/// </summary>
public interface ICountingService
{
    /// <summary>Current total count for the active session.</summary>
    int CurrentCount { get; }

    /// <summary>Starts a new counting session.</summary>
    Task<CountingSession> StartSessionAsync(string cameraId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a tracked frame, increments the count for newly crossed items,
    /// and notifies the PLC.
    /// </summary>
    Task ProcessFrameAsync(DetectionFrame frame, CancellationToken cancellationToken = default);

    /// <summary>Ends the current session and persists the result.</summary>
    Task<CountingSession> EndSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>Fires whenever the count changes.</summary>
    event EventHandler<int> CountChanged;
}
