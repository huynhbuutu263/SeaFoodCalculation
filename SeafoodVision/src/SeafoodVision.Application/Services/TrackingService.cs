using Microsoft.Extensions.Logging;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Application.Services;

/// <summary>
/// IoU-based centroid tracker.
/// Maintains a dictionary of active tracks and matches new detections
/// by maximising IoU using the Hungarian algorithm (greedy approximation).
/// </summary>
public sealed class TrackingService : ITrackingService
{
    private readonly ILogger<TrackingService> _logger;
    private readonly float _iouThreshold;
    private readonly TimeSpan _maxAge;

    private readonly Dictionary<TrackingId, SeafoodItem> _activeTracks = new();
    private int _nextId;

    public TrackingService(ILogger<TrackingService> logger, float iouThreshold = 0.3f, TimeSpan? maxAge = null)
    {
        _logger = logger;
        _iouThreshold = iouThreshold;
        _maxAge = maxAge ?? TimeSpan.FromSeconds(2);
    }

    public Task<IReadOnlyList<SeafoodItem>> UpdateAsync(
        IReadOnlyList<SeafoodItem> rawDetections,
        DateTime frameTimestamp,
        CancellationToken cancellationToken = default)
    {
        // Remove stale tracks
        var staleIds = _activeTracks
            .Where(kv => frameTimestamp - kv.Value.LastSeenAt > _maxAge)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var id in staleIds)
            _activeTracks.Remove(id);

        var matched = new HashSet<TrackingId>();

        foreach (var detection in rawDetections)
        {
            TrackingId? bestId = null;
            float bestIou = _iouThreshold;

            foreach (var (tid, track) in _activeTracks)
            {
                if (matched.Contains(tid)) continue;
                float iou = detection.BoundingBox.IntersectionOverUnion(track.BoundingBox);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    bestId = tid;
                }
            }

            if (bestId.HasValue)
            {
                _activeTracks[bestId.Value].UpdatePosition(detection.BoundingBox, frameTimestamp);
                matched.Add(bestId.Value);
            }
            else
            {
                var newId = new TrackingId(_nextId++);
                var newItem = new SeafoodItem(newId, detection.Label, detection.Confidence, detection.BoundingBox, frameTimestamp);
                _activeTracks[newId] = newItem;
            }
        }

        IReadOnlyList<SeafoodItem> result = _activeTracks.Values.ToList().AsReadOnly();
        _logger.LogDebug("Tracker updated: {ActiveTracks} active tracks", _activeTracks.Count);
        return Task.FromResult(result);
    }
}
