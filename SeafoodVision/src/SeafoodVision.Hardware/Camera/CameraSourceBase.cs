using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SeafoodVision.Hardware.Camera;

/// <summary>
/// Abstract base class that owns the Channel, the capture Task,
/// and the CancellationTokenSource.
/// Concrete sub-classes only need to implement CaptureLoopAsync.
/// </summary>
public abstract class CameraSourceBase : ICameraSource, IAsyncDisposable
{
    private readonly ILogger _logger;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private Channel<(long FrameIndex, DateTime CapturedAt, byte[] Data)>? _channel;

    protected CameraSourceBase(string cameraId, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
        CameraId = cameraId;
        _logger = logger;
    }

    public string CameraId { get; }
    public abstract CameraType CameraType { get; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _channel = Channel.CreateBounded<(long, DateTime, byte[])>(
            new BoundedChannelOptions(2)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = true
            });

        _captureTask = Task.Factory.StartNew(
            () => CaptureLoopAsync(_channel.Writer, _cts.Token).GetAwaiter().GetResult(),
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        _logger.LogInformation("[{Type}] Camera {Id} started", CameraType, CameraId);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        _channel?.Writer.TryComplete();
        if (_captureTask is not null)
            await _captureTask.ConfigureAwait(false);
        _logger.LogInformation("[{Type}] Camera {Id} stopped", CameraType, CameraId);
    }

    public async IAsyncEnumerable<(long FrameIndex, DateTime CapturedAt, byte[] Data)> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_channel is null) throw new InvalidOperationException("Camera not started.");
        await foreach (var frame in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return frame;
    }

    protected abstract Task CaptureLoopAsync(
        ChannelWriter<(long FrameIndex, DateTime CapturedAt, byte[] Data)> writer,
        CancellationToken ct);

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}