using Microsoft.Extensions.DependencyInjection;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Inspection.Services;

namespace SeafoodVision.Inspection;

public static class InspectionServiceRegistration
{
    public static IServiceCollection AddInspectionServices(this IServiceCollection services)
    {
        services.AddSingleton<IInspectionService, InspectionService>();
        return services;
    }
}

