using Microsoft.Extensions.DependencyInjection;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Inspection.Cache;
using SeafoodVision.Inspection.Services;
using SeafoodVision.Inspection.Pipeline;

namespace SeafoodVision.Inspection;

public static class InspectionServiceRegistration
{
    public static IServiceCollection AddInspectionServices(
        this IServiceCollection services)
    {
        // Pipeline runners (transient — each run gets a fresh instance)
        services.AddTransient<RoiPipelineRunner>();
        services.AddTransient<RecipePipelineRunner>();

        // IMemoryCache (AddMemoryCache is idempotent — safe to call multiple times)
        services.AddMemoryCache();

        // Recipe cache — singleton so the in-memory cache is shared across all requests
        services.AddSingleton<IRecipeCache, RecipeCacheService>();

        // Inspection service wired to the cache and pipeline
        services.AddSingleton<IInspectionService, InspectionService>();

        return services;
    }
}
