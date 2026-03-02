using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Hardware.Camera;
using SeafoodVision.Hardware.PLC;

namespace SeafoodVision.Hardware;

/// <summary>
/// Extension methods to register all Hardware services in the DI container.
/// </summary>
public static class HardwareServiceRegistration
{
    public static IServiceCollection AddHardware(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IFrameSource>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CameraFrameSource>>();
            var cameraId = configuration["Camera:Id"] ?? "CAM-01";
            var connection = configuration["Camera:ConnectionString"] ?? "0";
            return new CameraFrameSource(cameraId, connection, logger);
        });

        services.AddSingleton<IPLCService>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ModbusPLCService>>();
            var host = configuration["PLC:Host"] ?? "192.168.1.100";
            var port = int.Parse(configuration["PLC:Port"] ?? "502");
            var unitId = byte.Parse(configuration["PLC:UnitId"] ?? "1");
            var registerAddr = ushort.Parse(configuration["PLC:CountRegister"] ?? "40001");
            return new ModbusPLCService(host, port, unitId, registerAddr, logger);
        });

        return services;
    }
}
