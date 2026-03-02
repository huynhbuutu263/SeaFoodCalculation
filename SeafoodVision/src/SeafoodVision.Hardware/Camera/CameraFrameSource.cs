using Microsoft.Extensions.Logging;
using SeafoodVision.Domain.Interfaces;

namespace SeafoodVision.Hardware.Camera;

/// <summary>
/// Captures frames from a physical camera or RTSP stream using OpenCV bindings.
/// Runs on a dedicated background thread. Target: 30 FPS.
///
/// Threading model:
///   - A single dedicated Task performs blocking VideoCapture.Read().
///   - Frames are written to a System.Threading.Channels.Channel{T} (bounded, capacity 2).
///   - The consumer reads from ReadFramesAsync() on its own Task.
///   - Overflow frames are dropped to maintain real-time latency.
/// </summary>
public sealed class CameraFrameSource : IFrameSource
{
    private readonly string _connectionString;
    private readonly ILogger<CameraFrameSource> _logger;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private System.Threading.Channels.Channel<(long, DateTime, byte[])>? _channel;

    public string CameraId { get; }

    public CameraFrameSource(string cameraId, string connectionString, ILogger<CameraFrameSource> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        CameraId = cameraId;
        _connectionString = connectionString;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _channel = System.Threading.Channels.Channel.CreateBounded<(long, DateTime, byte[])>(
            new System.Threading.Channels.BoundedChannelOptions(2)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = true
            });

        _captureTask = Task.Factory.StartNew(
            () => CaptureLoop(_cts.Token),
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        _logger.LogInformation("Camera {CameraId} started ({Connection})", CameraId, _connectionString);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;
        await _cts.CancelAsync();
        _channel?.Writer.TryComplete();
        if (_captureTask is not null)
            await _captureTask.ConfigureAwait(false);
        _logger.LogInformation("Camera {CameraId} stopped", CameraId);
    }

    public async IAsyncEnumerable<(long FrameIndex, DateTime CapturedAt, byte[] Data)> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_channel is null) throw new InvalidOperationException("Camera not started.");
        await foreach (var frame in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return frame;
    }

    private void CaptureLoop(CancellationToken ct)
    {
        long frameIndex = 0;
        // NOTE: Replace the placeholder below with actual OpenCV/Emgu CV capture code.
        // e.g. using var capture = new VideoCapture(_connectionString);
        _logger.LogInformation("Capture loop started on thread {ThreadId}", Environment.CurrentManagedThreadId);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Placeholder: push empty frame bytes; real impl reads from VideoCapture.
                var frame = (frameIndex++, DateTime.UtcNow, Array.Empty<byte>());
                _channel!.Writer.TryWrite(frame);

                // Target 30 FPS → ~33 ms per frame
                // Use SpinWait or high-resolution timer instead of Thread.Sleep in prod.
                ct.WaitHandle.WaitOne(33);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _channel!.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
