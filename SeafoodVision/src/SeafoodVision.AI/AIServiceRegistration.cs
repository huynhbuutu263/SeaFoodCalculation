using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeafoodVision.AI.Client;
using SeafoodVision.Application.Interfaces;
using SeafoodVision.Domain.Interfaces;

namespace SeafoodVision.AI;

/// <summary>
/// Extension methods to register AI services in the DI container.
/// </summary>
public static class AIServiceRegistration
{
    public static IServiceCollection AddAIServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUrl = configuration["InferenceService:BaseUrl"] ?? "http://localhost:8000";

        services.AddHttpClient<InferenceHttpClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // Register as both IInferenceClient and IDetectionService
        services.AddScoped<IInferenceClient>(sp => sp.GetRequiredService<InferenceHttpClient>());
        services.AddScoped<IDetectionService>(sp => sp.GetRequiredService<InferenceHttpClient>());

        return services;
    }
}
