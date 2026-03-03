using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Domain.ValueObjects;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SeafoodVision.AI.Detection;

/// <summary>
/// In-process ONNX Runtime inference service, aligned with YOLOv8 ONNX export format.
///
/// Supports two YOLO output layouts (configurable via OnnxOptions.OutputLayout):
///   - Transposed [1, 6, 8400]  — default ultralytics export (yolo export format=onnx)
///   - Standard   [1, 8400, 6]  — older/custom exports
///
/// All tuning parameters (input size, class labels, thresholds, GPU) are driven
/// from OnnxOptions bound from appsettings.json "Onnx" section — no recompile needed.
/// </summary>
public sealed class OnnxDetectionService : IDetectionService, IAsyncDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly OnnxOptions _options;
    private readonly ILogger<OnnxDetectionService> _logger;

    public OnnxDetectionService(
        IOptions<OnnxOptions> options,
        ILogger<OnnxDetectionService> logger)
    {
        _logger = logger;
        _options = options.Value;

        var sessionOptions = new SessionOptions();

        if (_options.IntraOpNumThreads > 0)
            sessionOptions.IntraOpNumThreads = _options.IntraOpNumThreads;

        if (_options.UseGpu)
        {
            sessionOptions.AppendExecutionProvider_CUDA();
            _logger.LogInformation("ONNX: CUDA execution provider requested");
        }
        try
        {
            _session = new InferenceSession(_options.ModelPath, sessionOptions);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ONNX model from path: {Path}", _options.ModelPath);
            return;
        }

        if (_session.InputMetadata.Count == 0)
            throw new InvalidOperationException($"ONNX model at '{_options.ModelPath}' has no inputs.");

        _inputName = _session.InputMetadata.Keys.First();

        _logger.LogInformation(
            "ONNX model loaded. Input: {InputName}, Path: {Path}, Layout: {Layout}, Size: {W}x{H}",
            _inputName, _options.ModelPath, _options.OutputLayout, _options.InputWidth, _options.InputHeight);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SeafoodItem>> DetectAsync(
        byte[] frameData,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => RunInference(frameData), cancellationToken);
    }

    private IReadOnlyList<SeafoodItem> RunInference(byte[] frameData)
    {
        // 1. Decode and resize to model input size
        using var image = Image.Load<Rgb24>(frameData);
        int imgW = image.Width;
        int imgH = image.Height;
        image.Mutate(x => x.Resize(_options.InputWidth, _options.InputHeight));

        // 2. Build NCHW float32 tensor normalised to [0, 1]
        var tensor = new DenseTensor<float>(new[] { 1, 3, _options.InputHeight, _options.InputWidth });
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    var px = row[x];
                    tensor[0, 0, y, x] = px.R / 255f;
                    tensor[0, 1, y, x] = px.G / 255f;
                    tensor[0, 2, y, x] = px.B / 255f;
                }
            }
        });

        // 3. Run ONNX session
        var inputs = new List<NamedOnnxValue>
            { NamedOnnxValue.CreateFromTensor(_inputName, tensor) };

        using var outputs = _session.Run(inputs);
        var predictions = outputs[0].AsTensor<float>();

        // 4. Parse output — handle both YOLO output layouts
        var results = _options.OutputLayout == YoloOutputLayout.Transposed
            ? ParseTransposed(predictions, imgW, imgH)   // [1, 6, 8400]
            : ParseStandard(predictions, imgW, imgH);    // [1, 8400, 6]

        _logger.LogDebug("ONNX inference returned {Count} detections", results.Count);
        return results;
    }

    /// <summary>
    /// Parses YOLO transposed output: [1, num_fields, num_detections]
    /// Default format from: yolo export model=best.pt format=onnx
    /// Fields: [x1, y1, x2, y2, conf, class_id] — corner-based (xyxy) format after NMS.
    /// </summary>
    private IReadOnlyList<SeafoodItem> ParseTransposed(Tensor<float> predictions, int imgW, int imgH)
    {
        if (predictions.Dimensions.Length < 3)
        {
            _logger.LogWarning("Unexpected transposed ONNX output shape; returning empty detections");
            return Array.Empty<SeafoodItem>();
        }

        int numDetections = predictions.Dimensions[2];
        var results = new List<SeafoodItem>();
        int tmpId = 0;

        for (int i = 0; i < numDetections && results.Count < _options.MaxDetections; i++)
        {
            float x1         = predictions[0, 0, i];
            float y1         = predictions[0, 1, i];
            float x2         = predictions[0, 2, i];
            float y2         = predictions[0, 3, i];
            float confidence = predictions[0, 4, i];
            int   classId    = (int)predictions[0, 5, i];

            if (confidence < _options.ConfidenceThreshold)
                continue;

            var label = GetLabel(classId);
            var box = new BoundingBox(
                X:      x1 / imgW,
                Y:      y1 / imgH,
                Width:  (x2 - x1) / imgW,
                Height: (y2 - y1) / imgH);

            results.Add(new SeafoodItem(new TrackingId(tmpId++), label, confidence, box, DateTime.UtcNow));
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Parses standard output: [1, num_detections, num_fields]
    /// Older/custom ONNX exports.
    /// </summary>
    private IReadOnlyList<SeafoodItem> ParseStandard(Tensor<float> predictions, int imgW, int imgH)
    {
        if (predictions.Dimensions.Length < 2 || predictions.Dimensions[0] < 1)
        {
            _logger.LogWarning("Unexpected standard ONNX output shape; returning empty detections");
            return Array.Empty<SeafoodItem>();
        }

        int numDetections = predictions.Dimensions[1];
        var results = new List<SeafoodItem>();
        int tmpId = 0;

        for (int i = 0; i < numDetections && results.Count < _options.MaxDetections; i++)
        {
            float x1         = predictions[0, i, 0];
            float y1         = predictions[0, i, 1];
            float x2         = predictions[0, i, 2];
            float y2         = predictions[0, i, 3];
            float confidence = predictions[0, i, 4];
            int   classId    = (int)predictions[0, i, 5];

            if (confidence < _options.ConfidenceThreshold)
                continue;

            var label = GetLabel(classId);
            var box = new BoundingBox(
                X:      x1 / imgW,
                Y:      y1 / imgH,
                Width:  (x2 - x1) / imgW,
                Height: (y2 - y1) / imgH);

            results.Add(new SeafoodItem(new TrackingId(tmpId++), label, confidence, box, DateTime.UtcNow));
        }

        return results.AsReadOnly();
    }

    private string GetLabel(int classId)
        => (classId >= 0 && classId < _options.ClassLabels.Count)
            ? _options.ClassLabels[classId]
            : "unknown";

    public ValueTask DisposeAsync()
    {
        _session.Dispose();
        return ValueTask.CompletedTask;
    }
}
