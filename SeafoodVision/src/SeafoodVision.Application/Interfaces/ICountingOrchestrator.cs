using SeafoodVision.Application.DTOs;

namespace SeafoodVision.Application.Interfaces;

/// <summary>
/// Top-level pipeline orchestrator. Wires frame acquisition → detection → tracking → counting.
/// Implemented in the Application layer; consumed by the Presentation layer.
/// </summary>
public interface ICountingOrchestrator : IAsyncDisposable
{
    /// <summary>Starts the full pipeline for the specified camera.</summary>
    Task StartAsync(string cameraId, CancellationToken cancellationToken = default);

    /// <summary>Stops the pipeline and finalises the session.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a snapshot of the current session state.</summary>
    CountingSessionDto? CurrentSession { get; }

    /// <summary>Fires on every new counting event with the updated total.</summary>
    event EventHandler<int> CountUpdated;
}
