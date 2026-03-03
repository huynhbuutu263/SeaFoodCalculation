using Microsoft.Extensions.Logging;
using SeafoodVision.Application.DTOs;
using SeafoodVision.Application.Interfaces;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;

namespace SeafoodVision.Application.Services;

/// <summary>
/// Orchestrates the real-time counting pipeline:
/// FrameSource → AI Detection → C# Tracking → Counting → PLC notification.
///
/// Threading model:
///   - Frame acquisition runs on a dedicated background Task (producer).
///   - Detection + tracking run in parallel via Channel&lt;T&gt; (consumer).
///   - All PLC writes are fire-and-forget with structured error logging.
/// </summary>
public sealed class CountingOrchestrator : ICountingOrchestrator
{
    private readonly IFrameSource _frameSource;
    private readonly IDetectionService _detectionService;
    private readonly IInspectionService _inspectionService;
    private readonly ITrackingService _trackingService;
    private readonly ICountingService _countingService;
    private readonly ILogger<CountingOrchestrator> _logger;

    private CancellationTokenSource? _cts;
    private Task? _pipelineTask;
    private CountingSession? _session;

    public CountingSessionDto? CurrentSession =>
        _session is null ? null
        : new CountingSessionDto(_session.Id, _session.CameraId,
            _session.StartedAt, _session.EndedAt, _session.TotalCount);

    public event EventHandler<int>? CountUpdated;

    public CountingOrchestrator(
        IFrameSource frameSource,
        IDetectionService detectionService,
        IInspectionService inspectionService,
        ITrackingService trackingService,
        ICountingService countingService,
        ILogger<CountingOrchestrator> logger)
    {
        _frameSource = frameSource;
        _detectionService = detectionService;
        _inspectionService = inspectionService;
        _trackingService = trackingService;
        _countingService = countingService;
        _logger = logger;

        _countingService.CountChanged += (_, count) => CountUpdated?.Invoke(this, count);
    }

    public async Task StartAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _session = await _countingService.StartSessionAsync(cameraId, _cts.Token);
        await _frameSource.StartAsync(_cts.Token);
        _pipelineTask = RunPipelineAsync(_cts.Token);
        _logger.LogInformation("Pipeline started for camera {CameraId}", cameraId);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;
        await _cts.CancelAsync();
        if (_pipelineTask is not null)
            await _pipelineTask.ConfigureAwait(false);
        await _frameSource.StopAsync(cancellationToken);
        _session = await _countingService.EndSessionAsync(cancellationToken);
        _logger.LogInformation("Pipeline stopped. Final count: {Count}", _session.TotalCount);
    }

    private async Task RunPipelineAsync(CancellationToken ct)
    {
        await foreach (var (frameIndex, capturedAt, data) in _frameSource.ReadFramesAsync(ct))
        {
            try
            {
                var rawDetections = await _detectionService.DetectAsync(data, ct);
                var inspectedDetections = await _inspectionService.InspectAsync(data, rawDetections, ct);
                var trackedItems = await _trackingService.UpdateAsync(inspectedDetections, capturedAt, ct);
                var frame = new DetectionFrame(frameIndex, capturedAt, trackedItems);
                await _countingService.ProcessFrameAsync(frame, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing frame {FrameIndex}", frameIndex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
