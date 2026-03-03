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

    /// <summary>Minimum detection confidence threshold (mirrors Python CONFIDENCE_THRESHOLD env var).</summary>
    public float ConfidenceThreshold { get; set; } = 0.5f;

    /// <summary>Set true to request CUDA execution provider (NVIDIA GPU). Falls back to CPU automatically.</summary>
    public bool UseGpu { get; set; } = false;
}
