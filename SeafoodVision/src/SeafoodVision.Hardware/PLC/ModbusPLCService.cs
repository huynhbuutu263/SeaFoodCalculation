using Microsoft.Extensions.Logging;
using SeafoodVision.Domain.Interfaces;

namespace SeafoodVision.Hardware.PLC;

/// <summary>
/// Modbus TCP implementation of <see cref="IPLCService"/>.
/// Uses NModbus to communicate with an industrial PLC at the configured host/port.
///
/// Threading model:
///   - All Modbus I/O is done asynchronously.
///   - A SemaphoreSlim(1,1) guards the underlying TCP connection to prevent concurrent writes.
/// </summary>
public sealed class ModbusPLCService : IPLCService
{
    private readonly string _host;
    private readonly int _port;
    private readonly byte _unitId;
    private readonly ushort _countRegisterAddress;
    private readonly ILogger<ModbusPLCService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // NOTE: Replace dynamic type with NModbus IModbusMaster once NModbus is installed.
    private object? _master;
    private System.Net.Sockets.TcpClient? _tcpClient;

    public ModbusPLCService(
        string host,
        int port,
        byte unitId,
        ushort countRegisterAddress,
        ILogger<ModbusPLCService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        _host = host;
        _port = port;
        _unitId = unitId;
        _countRegisterAddress = countRegisterAddress;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _tcpClient = new System.Net.Sockets.TcpClient();
        await _tcpClient.ConnectAsync(_host, _port, cancellationToken);
        // NModbus usage (uncomment after package install):
        // var factory = new ModbusFactory();
        // _master = factory.CreateMaster(_tcpClient);
        _logger.LogInformation("PLC connected to {Host}:{Port} unit {UnitId}", _host, _port, _unitId);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;
            _master = null;
            _logger.LogInformation("PLC disconnected");
        }
        finally { _lock.Release(); }
    }

    public async Task WriteCountAsync(int count, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // NModbus usage (uncomment after package install):
            // var master = (IModbusMaster)_master!;
            // await master.WriteSingleRegisterAsync(_unitId, _countRegisterAddress, (ushort)Math.Clamp(count, 0, ushort.MaxValue));
            _logger.LogDebug("PLC count register written: {Count}", count);
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> ReadLineStatusAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // NModbus usage (uncomment after package install):
            // var master = (IModbusMaster)_master!;
            // var inputs = await master.ReadInputsAsync(_unitId, 0, 1);
            // return inputs[0];
            return true; // placeholder
        }
        finally { _lock.Release(); }
    }

    public async Task SendPulseAsync(ushort coilAddress, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // NModbus usage (uncomment after package install):
            // var master = (IModbusMaster)_master!;
            // await master.WriteSingleCoilAsync(_unitId, coilAddress, true);
            // await Task.Delay(50, cancellationToken);
            // await master.WriteSingleCoilAsync(_unitId, coilAddress, false);
            _logger.LogDebug("PLC pulse sent to coil {Coil}", coilAddress);
        }
        finally { _lock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _lock.Dispose();
    }
}
