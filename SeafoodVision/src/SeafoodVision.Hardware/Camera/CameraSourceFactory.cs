using Microsoft.Extensions.Logging;

namespace SeafoodVision.Hardware.Camera;

/// <summary>
/// Creates the correct ICameraSource implementation based on CameraOptions.Type.
/// </summary>
public static class CameraSourceFactory
{
    public static ICameraSource Create(CameraOptions options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        return options.Type switch
        {
            CameraType.Usb => new UsbCameraSource(
                options.Id, options.ConnectionString,
                loggerFactory.CreateLogger<UsbCameraSource>()),

            CameraType.Hik => new HikCameraSource(
                options.Id, options.ConnectionString,
                loggerFactory.CreateLogger<HikCameraSource>()),

            CameraType.Basler => new BaslerCameraSource(
                options.Id, options.ConnectionString,
                loggerFactory.CreateLogger<BaslerCameraSource>()),

            _ => throw new NotSupportedException(
                $"Camera type '{options.Type}' is not supported. " +
                $"Valid values: {string.Join(", ", Enum.GetNames<CameraType>())}")
        };
    }
}