using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Threading.Channels;

namespace SeafoodVision.Hardware.Camera;

/// <summary>
/// Captures frames from a USB / DirectShow / RTSP source via OpenCV.
/// Replace the stub body with real OpenCvSharp VideoCapture calls.
/// </summary>
public sealed class UsbCameraSource : CameraSourceBase
{
    private readonly string _connectionString;

    public UsbCameraSource(string cameraId, string connectionString,
        ILogger<UsbCameraSource> logger)
        : base(cameraId, logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public override CameraType CameraType => CameraType.Usb;

    protected override async Task CaptureLoopAsync(
        ChannelWriter<(long FrameIndex, DateTime CapturedAt, byte[] Data)> writer,
        CancellationToken ct)
    {
        long frameIndex = 0;
        using var capture = new VideoCapture(_connectionString);
        try
        {
            var mat = new Mat();
            while (!ct.IsCancellationRequested)
            {
                capture.Read(mat);
                if (mat.Empty()) continue;
                byte[] bytes = mat.ImEncode(".jpg");
                await writer.WriteAsync((frameIndex++, DateTime.UtcNow, bytes), ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { writer.TryComplete(); }
    }
}