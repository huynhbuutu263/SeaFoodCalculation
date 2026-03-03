namespace SeafoodVision.AI.Detection;

/// <summary>
/// Configuration options for the in-process ONNX inference service.
/// Bound from the "Onnx" section in appsettings.json.
/// </summary>
public sealed class OnnxOptions
{
    public const string SectionName = "Onnx";

    /// <summary>Path to the .onnx model file.</summary>
    public string ModelPath { get; set; } = "models/seafood_detector.onnx";

    /// <summary>Minimum detection confidence threshold.</summary>
    public float ConfidenceThreshold { get; set; } = 0.5f;

    /// <summary>IoU threshold for Non-Maximum Suppression (NMS).</summary>
    public float IouThreshold { get; set; } = 0.45f;

    /// <summary>Maximum number of detections to return per frame.</summary>
    public int MaxDetections { get; set; } = 100;

    /// <summary>Model input width in pixels (must match the exported ONNX model).</summary>
    public int InputWidth { get; set; } = 640;

    /// <summary>Model input height in pixels (must match the exported ONNX model).</summary>
    public int InputHeight { get; set; } = 640;

    /// <summary>
    /// YOLO ONNX output layout.
    /// Transposed = [1, 6+, 8400] — default from ultralytics export.
    /// Standard   = [1, 8400, 6+] — older or custom exports.
    /// </summary>
    public YoloOutputLayout OutputLayout { get; set; } = YoloOutputLayout.Transposed;

    /// <summary>
    /// Class labels matching the model's output indices.
    /// Override in appsettings.json to match your trained model.
    /// </summary>
    public List<string> ClassLabels { get; set; } =
    [
        "background", "salmon", "tuna", "shrimp",
        "crab", "squid", "octopus", "mackerel", "cod", "tilapia"
    ];

    /// <summary>Set true to request CUDA execution provider (NVIDIA GPU). Falls back to CPU automatically.</summary>
    public bool UseGpu { get; set; } = false;

    /// <summary>Number of intra-op threads (0 = use ORT default).</summary>
    public int IntraOpNumThreads { get; set; } = 0;
}

public enum YoloOutputLayout
{
    /// <summary>[1, num_classes+5, num_detections] — default ultralytics ONNX export</summary>
    Transposed,
    /// <summary>[1, num_detections, num_classes+5] — older/custom exports</summary>
    Standard
}
