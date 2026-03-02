using SeafoodVision.Application.DTOs;

namespace SeafoodVision.Application.Interfaces;

/// <summary>
/// Application-level interface that wraps the Python FastAPI inference endpoint.
/// The AI layer implements this; the Application layer only depends on the interface.
/// </summary>
public interface IInferenceClient
{
    /// <summary>
    /// Sends raw frame bytes to the Python inference service and returns detection results.
    /// </summary>
    Task<IReadOnlyList<DetectionResultDto>> InferAsync(byte[] frameData, CancellationToken cancellationToken = default);

    /// <summary>Checks that the inference service is healthy and reachable.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
