using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace SeafoodVision.Hardware.Camera;

/// <summary>
/// Captures frames from a Basler camera via the Basler Pylon .NET SDK.
///
/// Connection string accepted formats:
///   • Device serial number, e.g. "22591867"
///   • "first" — opens the first available Basler camera
///
/// SDK stub: replace the placeholder body with real
/// <c>Basler.Pylon.Camera</c> calls once the
/// Basler.Pylon NuGet package is referenced.
/// </summary>
public sealed class BaslerCameraSource : CameraSourceBase
{
    private readonly string _connectionString;

    public BaslerCameraSource(string cameraId, string connectionString,
        ILogger<BaslerCameraSource> logger)
        : base(cameraId, logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    /// <inheritdoc/>
    public override CameraType CameraType => CameraType.Basler;

    /// <inheritdoc/>
    protected override async Task CaptureLoopAsync(
        ChannelWriter<(long FrameIndex, DateTime CapturedAt, byte[] Data)> writer,
        CancellationToken ct)
    {
        long frameIndex = 0;

        // TODO: replace with Basler Pylon SDK calls
        //
        // using var camera = string.Equals(_connectionString, "first",
        //     StringComparison.OrdinalIgnoreCase)
        //     ? new Camera()                               // opens first device
        //     : new Camera(DeviceLocator.Create(_connectionString));
        //
        // camera.Open();
        // camera.StreamGrabber.Start();
        //
        // while (!ct.IsCancellationRequested)
        // {
        //     using IGrabResult result = camera.StreamGrabber.RetrieveResult(
        //         5_000, TimeoutHandling.ThrowException);
        //
        //     if (result.GrabSucceeded)
        //     {
        //         byte[] bytes = result.PixelData as byte[] ?? Array.Empty<byte>();
        //         await writer.WriteAsync((frameIndex++, DateTime.UtcNow, bytes), ct);
        //     }
        // }
        //
        // camera.StreamGrabber.Stop();
        // camera.Close();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await writer.WriteAsync(
                    (frameIndex++, DateTime.UtcNow, Array.Empty<byte>()),
                    ct).ConfigureAwait(false);

                await Task.Delay(33, ct).ConfigureAwait(false); // ~30 FPS
            }
        }
        catch (OperationCanceledException) { }
        finally { writer.TryComplete(); }
    }
}