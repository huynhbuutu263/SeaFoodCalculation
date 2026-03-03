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
/// In-process ONNX Runtime inference service.
/// Replaces <see cref="SeafoodVision.AI.Client.InferenceHttpClient"/> (HTTP → Python FastAPI).
///
/// Mirrors the logic of inference_service.py _detect_sync() exactly:
///   - Decodes JPEG/PNG frame bytes via ImageSharp (replaces PIL)
///   - Resizes to 640×640
///   - Normalises to [0,1] float32 NCHW tensor
///   - Runs ONNX session
///   - Parses [1, num_detections, 6] output (x1,y1,x2,y2,conf,class_id)
///   - Filters by confidence threshold
///   - Normalises bounding box coordinates to [0,1]
///
/// Threading: InferenceSession is thread-safe. RunInference() runs on the
/// thread-pool via Task.Run() to keep the async pipeline non-blocking,
/// matching Python's loop.run_in_executor() pattern.
/// </summary>
public sealed class OnnxDetectionService : IDetectionService, IAsyncDisposable
{
    // Must match CLASS_LABELS in inference_service.py
    private static readonly string[] ClassLabels =
    [
        "background", "salmon", "tuna", "shrimp",
        "crab", "squid", "octopus", "mackerel", "cod", "tilapia"
    ];

    private const int InputWidth  = 640;
    private const int InputHeight = 640;

    private readonly InferenceSession _session;
    private readonly string           _inputName;
    private readonly float            _confidenceThreshold;
    private readonly ILogger<OnnxDetectionService> _logger;

    public OnnxDetectionService(
        IOptions<OnnxOptions> options,
        ILogger<OnnxDetectionService> logger)
    {
        _logger = logger;
        _confidenceThreshold = options.Value.ConfidenceThreshold;

        var sessionOptions = new SessionOptions();

        if (options.Value.UseGpu)
        {
            sessionOptions.AppendExecutionProvider_CUDA();
            _logger.LogInformation("ONNX: CUDA execution provider requested");
        }

        _session = new InferenceSession(options.Value.ModelPath, sessionOptions);

        if (_session.InputMetadata.Count == 0)
            throw new InvalidOperationException($"ONNX model at '{options.Value.ModelPath}' has no inputs.");

        _inputName = _session.InputMetadata.Keys.First();

        _logger.LogInformation(
            "ONNX model loaded in-process. Input: {InputName}, Path: {Path}",
            _inputName, options.Value.ModelPath);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SeafoodItem>> DetectAsync(
        byte[] frameData,
        CancellationToken cancellationToken = default)
    {
        // Offload CPU-bound inference to thread-pool (mirrors Python run_in_executor)
        return Task.Run(() => RunInference(frameData), cancellationToken);
    }

    private IReadOnlyList<SeafoodItem> RunInference(byte[] frameData)
    {
        // 1. Decode and resize  (PIL Image.open().convert("RGB") + resize(640,640) in Python)
        using var image = Image.Load<Rgb24>(frameData);
        int imgW = image.Width;
        int imgH = image.Height;
        image.Mutate(x => x.Resize(InputWidth, InputHeight));

        // 2. Build NCHW float32 tensor normalised to [0,1]  (numpy array / 255.0 + transpose in Python)
        var tensor = new DenseTensor<float>(new[] { 1, 3, InputHeight, InputWidth });
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

        // 3. Run ONNX session  (self._session.run(None, {self._input_name: tensor}) in Python)
        var inputs = new List<NamedOnnxValue>
            { NamedOnnxValue.CreateFromTensor(_inputName, tensor) };

        using var outputs = _session.Run(inputs);

        // 4. Parse output [1, num_detections, 6] → (x1, y1, x2, y2, conf, class_id)
        var predictions = outputs[0].AsTensor<float>();
        if (predictions.Dimensions.Length < 2 || predictions.Dimensions[0] < 1)
        {
            _logger.LogWarning("Unexpected ONNX output shape; returning empty detections");
            return Array.Empty<SeafoodItem>();
        }

        int numDetections = predictions.Dimensions[1];

        var results = new List<SeafoodItem>();
        int tmpId   = 0;

        for (int i = 0; i < numDetections; i++)
        {
            float x1         = predictions[0, i, 0];
            float y1         = predictions[0, i, 1];
            float x2         = predictions[0, i, 2];
            float y2         = predictions[0, i, 3];
            float confidence = predictions[0, i, 4];
            int   classId    = (int)predictions[0, i, 5];

            if (confidence < _confidenceThreshold)
                continue;

            var label = (classId >= 0 && classId < ClassLabels.Length) ? ClassLabels[classId] : "unknown";

            // Normalise coordinates to [0,1] (mirrors float(x1)/img_w in Python)
            var box = new BoundingBox(
                X:      x1 / imgW,
                Y:      y1 / imgH,
                Width:  (x2 - x1) / imgW,
                Height: (y2 - y1) / imgH);

            results.Add(new SeafoodItem(
                new TrackingId(tmpId++),
                label,
                confidence,
                box,
                DateTime.UtcNow));
        }

        _logger.LogDebug("ONNX in-process inference returned {Count} detections", results.Count);
        return results.AsReadOnly();
    }

    public ValueTask DisposeAsync()
    {
        _session.Dispose();
        return ValueTask.CompletedTask;
    }
}
