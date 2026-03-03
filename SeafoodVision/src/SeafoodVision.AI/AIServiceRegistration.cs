using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeafoodVision.AI.Detection;
using SeafoodVision.Domain.Interfaces;

namespace SeafoodVision.AI;

/// <summary>
/// Extension methods to register AI services in the DI container.
/// Registers <see cref="OnnxDetectionService"/> as a singleton in-process
/// ONNX inference engine, replacing the HTTP-based InferenceHttpClient.
/// </summary>
public static class AIServiceRegistration
{
    public static IServiceCollection AddAIServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind OnnxOptions from appsettings.json "Onnx" section
        services.Configure<OnnxOptions>(
            configuration.GetSection(OnnxOptions.SectionName));

        // Singleton: InferenceSession is expensive to construct and thread-safe for Run()
        services.AddSingleton<OnnxDetectionService>();
        services.AddSingleton<IDetectionService>(
            sp => sp.GetRequiredService<OnnxDetectionService>());

        return services;
    }
}
