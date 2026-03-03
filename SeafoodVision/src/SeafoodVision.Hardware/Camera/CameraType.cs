namespace SeafoodVision.Hardware.Camera;

/// <summary>
/// Identifies which SDK/transport is used to grab frames.
/// </summary>
public enum CameraType
{
    /// <summary>Standard USB / DirectShow / V4L2 camera accessed via OpenCV.</summary>
    Usb = 0,

    /// <summary>HIK (Hikvision / HIKROBOT) camera via the MvCameraControl SDK.</summary>
    Hik = 1,

    /// <summary>Basler camera via the Basler Pylon SDK.</summary>
    Basler = 2
}