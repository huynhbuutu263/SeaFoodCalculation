using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Inspection.Core;

namespace SeafoodVision.Inspection.Services;

public class InspectionService : IInspectionService
{
    private readonly ILogger<InspectionService> _logger;

    public InspectionService(ILogger<InspectionService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<SeafoodItem>> InspectAsync(
        byte[] frameData,
        IReadOnlyList<SeafoodItem> detections,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Running inspection on {Count} detections.", detections.Count);

        // Decode frame once for all inspection rules.
        using var bgr = Cv2.ImDecode(frameData, ImreadModes.Color);
        if (bgr.Empty())
        {
            _logger.LogWarning("Inspection skipped: failed to decode frame.");
            return Task.FromResult(detections);
        }

        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        // Example 1: threshold + morphology (basic segmentation)
        using var binary = ImageThresholding.ToBinary(gray);
        using var opened = ImageMorphology.Open(binary, kernelSize: 3, iterations: 1);

        // Example 2: template matching (hook for future templates)
        // You can load template images from disk and call TemplateMatcher.FindMatches(...)
        // to verify presence/absence or position of specific patterns on the belt/product.

        // For now we don't alter detections; this is your extension point
        // to filter or adjust the list based on inspection rules.
        return Task.FromResult(detections);
    }
}

