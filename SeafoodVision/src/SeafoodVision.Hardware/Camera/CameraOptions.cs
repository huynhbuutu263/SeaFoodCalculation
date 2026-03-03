namespace SeafoodVision.Hardware.Camera;

/// <summary>
/// Strongly-typed configuration options for any camera source.
/// Bound from the "Camera" section of appsettings.json.
/// </summary>
public sealed class CameraOptions
{
    public const string SectionName = "Camera";

    /// <summary>Logical camera identifier, e.g. "CAM-01".</summary>
    public string Id { get; set; } = "CAM-01";

    /// <summary>
    /// For <see cref="CameraType.Usb"/>    : OpenCV device index or RTSP URL.
    /// For <see cref="CameraType.Hik"/>    : device serial number or IP address.
    /// For <see cref="CameraType.Basler"/> : Pylon device serial or "first".
    /// </summary>
    public string ConnectionString { get; set; } = "0";

    /// <summary>Selects which camera SDK/transport to use.</summary>
    public CameraType Type { get; set; } = CameraType.Usb;
}