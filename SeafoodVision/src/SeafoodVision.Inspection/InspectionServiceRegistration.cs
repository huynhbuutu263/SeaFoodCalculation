using Microsoft.Extensions.DependencyInjection;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Inspection.Services;
using SeafoodVision.Inspection.Pipeline;

namespace SeafoodVision.Inspection;

public static class InspectionServiceRegistration
{
    public static IServiceCollection AddInspectionServices(this IServiceCollection services)
    {
        services.AddTransient<RoiPipelineRunner>();
        services.AddTransient<RecipePipelineRunner>();
        services.AddSingleton<IInspectionService, InspectionService>();

        return services;
    }
}
