using SeafoodVision.Domain.Interfaces;

namespace SeafoodVision.Hardware.Camera;

/// <summary>
/// Extends <see cref="IFrameSource"/> with camera-type metadata.
/// All concrete camera adapters (USB, HIK, Basler) implement this interface.
/// </summary>
public interface ICameraSource : IFrameSource
{
    /// <summary>Identifies the physical/SDK camera type.</summary>
    CameraType CameraType { get; }
}