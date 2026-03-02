namespace SeafoodVision.Domain.Interfaces;

/// <summary>
/// Provides raw camera frames for downstream processing.
/// Implementations must be non-blocking and thread-safe.
/// </summary>
public interface IFrameSource : IAsyncDisposable
{
    /// <summary>Gets the camera identifier.</summary>
    string CameraId { get; }

    /// <summary>Starts the camera capture pipeline.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the camera capture pipeline.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Yields encoded JPEG/PNG frames as byte arrays at the configured frame rate.
    /// </summary>
    IAsyncEnumerable<(long FrameIndex, DateTime CapturedAt, byte[] Data)> ReadFramesAsync(CancellationToken cancellationToken = default);
}
