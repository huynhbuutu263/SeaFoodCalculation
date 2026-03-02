using Microsoft.Extensions.Logging;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;

namespace SeafoodVision.Application.Services;

/// <summary>
/// Counts items that cross a virtual horizontal counting line.
/// Notifies the PLC on every new count increment.
/// </summary>
public sealed class CountingService : ICountingService
{
    private readonly ISessionRepository _repository;
    private readonly IPLCService _plcService;
    private readonly ILogger<CountingService> _logger;

    // Normalised Y-coordinate of the counting line (0.0 = top, 1.0 = bottom)
    private const float CountingLineY = 0.5f;

    private CountingSession? _session;
    private readonly HashSet<int> _countedIds = new();

    public int CurrentCount => _session?.TotalCount ?? 0;

    public event EventHandler<int>? CountChanged;

    public CountingService(ISessionRepository repository, IPLCService plcService, ILogger<CountingService> logger)
    {
        _repository = repository;
        _plcService = plcService;
        _logger = logger;
    }

    public async Task<CountingSession> StartSessionAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        _countedIds.Clear();
        _session = CountingSession.Start(cameraId);
        await _repository.AddAsync(_session, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Session {SessionId} started for camera {CameraId}", _session.Id, cameraId);
        return _session;
    }

    public async Task ProcessFrameAsync(DetectionFrame frame, CancellationToken cancellationToken = default)
    {
        if (_session is null) throw new InvalidOperationException("No active session.");

        int newItems = 0;
        foreach (var item in frame.Detections)
        {
            if (_countedIds.Contains(item.TrackingId.Value)) continue;
            if (item.BoundingBox.CenterY >= CountingLineY)
            {
                _countedIds.Add(item.TrackingId.Value);
                item.MarkAsCounted();
                _session.IncrementCount();
                newItems++;
            }
        }

        if (newItems > 0)
        {
            await _repository.UpdateAsync(_session, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            await _plcService.WriteCountAsync(_session.TotalCount, cancellationToken);
            CountChanged?.Invoke(this, _session.TotalCount);
            _logger.LogInformation("Count updated to {Count} (+{Delta})", _session.TotalCount, newItems);
        }
    }

    public async Task<CountingSession> EndSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null) throw new InvalidOperationException("No active session.");
        _session.End();
        await _repository.UpdateAsync(_session, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Session {SessionId} ended. Total: {Count}", _session.Id, _session.TotalCount);
        return _session;
    }
}
