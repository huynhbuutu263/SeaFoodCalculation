using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;

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
        // Placeholder implementation: just log and return detections unchanged.
        // You can later plug in OpenCV-based inspection and rule logic here.
        _logger.LogDebug("Running inspection on {Count} detections.", detections.Count);

        // Example of where you might decode the frame for inspection:
        // using var mat = Cv2.ImDecode(frameData, ImreadModes.Color);
        // TODO: apply OpenCV / rule-based checks against mat and detections.

        return Task.FromResult(detections);
    }
}

