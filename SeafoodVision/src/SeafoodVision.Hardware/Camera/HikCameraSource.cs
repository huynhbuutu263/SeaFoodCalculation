using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace SeafoodVision.Hardware.Camera;

/// <summary>
/// Captures frames from a HIK (Hikvision / HIKROBOT) industrial camera
/// via the MvCameraControl .NET SDK.
/// Replace the stub body with real MvCameraControl API calls.
/// </summary>
public sealed class HikCameraSource : CameraSourceBase
{
    private readonly string _connectionString;

    public HikCameraSource(string cameraId, string connectionString,
        ILogger<HikCameraSource> logger)
        : base(cameraId, logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public override CameraType CameraType => CameraType.Hik;

    protected override async Task CaptureLoopAsync(
        ChannelWriter<(long FrameIndex, DateTime CapturedAt, byte[] Data)> writer,
        CancellationToken ct)
    {
        long frameIndex = 0;

        // TODO: replace with HIK MvCameraControl SDK calls
        // MyCamera camera = new MyCamera();
        // MvCamCtrl.nGetDeviceList(MV_GIGE_DEVICE | MV_USB_DEVICE, ref stDeviceList);
        // camera.MV_CC_CreateHandle_NET(ref stDevInfo);
        // camera.MV_CC_OpenDevice_NET();
        // camera.MV_CC_StartGrabbing_NET();
        // while (!ct.IsCancellationRequested)
        // {
        //     MyCamera.MV_FRAME_OUT stFrameOut = new();
        //     int ret = camera.MV_CC_GetImageBuffer_NET(ref stFrameOut, 1000);
        //     if (ret == MyCamera.MV_OK)
        //     {
        //         byte[] bytes = ...; // copy from stFrameOut.pBufAddr
        //         camera.MV_CC_FreeImageBuffer_NET(ref stFrameOut);
        //         await writer.WriteAsync((frameIndex++, DateTime.UtcNow, bytes), ct);
        //     }
        // }
        // camera.MV_CC_StopGrabbing_NET();
        // camera.MV_CC_CloseDevice_NET();
        // camera.MV_CC_DestroyHandle_NET();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await writer.WriteAsync(
                    (frameIndex++, DateTime.UtcNow, Array.Empty<byte>()), ct)
                    .ConfigureAwait(false);
                await Task.Delay(33, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        finally { writer.TryComplete(); }
    }
}