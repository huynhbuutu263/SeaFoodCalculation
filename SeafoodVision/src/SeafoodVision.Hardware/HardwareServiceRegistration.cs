using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        // Camera: read CameraOptions from config, create the right adapter via factory,
        // expose both ICameraSource and IFrameSource as the same singleton.
        services.AddSingleton<ICameraSource>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var options = new CameraOptions();
            configuration.GetSection(CameraOptions.SectionName).Bind(options);
            return CameraSourceFactory.Create(options, loggerFactory);
        });

        services.AddSingleton<IFrameSource>(sp =>
            sp.GetRequiredService<ICameraSource>());

        // PLC
        services.AddSingleton<IPLCService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ModbusPLCService>>();
            var host = configuration["PLC:Host"] ?? "192.168.1.100";
            var port = int.Parse(configuration["PLC:Port"] ?? "502");
            var unitId = byte.Parse(configuration["PLC:UnitId"] ?? "1");
            var registerAddr = ushort.Parse(configuration["PLC:CountRegister"] ?? "40001");
            return new ModbusPLCService(host, port, unitId, registerAddr, logger);
        });

        return services;
    }
}