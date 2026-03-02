namespace SeafoodVision.Domain.Interfaces;

/// <summary>
/// Abstracts communication with a Programmable Logic Controller over Modbus TCP.
/// All methods are async to keep the pipeline non-blocking.
/// </summary>
public interface IPLCService : IAsyncDisposable
{
    /// <summary>Establishes the TCP connection to the PLC.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Gracefully disconnects from the PLC.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the current seafood count to a holding register.</summary>
    Task WriteCountAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Reads a digital input to check conveyor/line status.</summary>
    Task<bool> ReadLineStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends a pulse signal to the PLC (e.g. item-counted acknowledgement).</summary>
    Task SendPulseAsync(ushort coilAddress, CancellationToken cancellationToken = default);
}
