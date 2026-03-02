using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Infrastructure.Data;
using SeafoodVision.Infrastructure.Data.Repositories;

namespace SeafoodVision.Infrastructure;

/// <summary>
/// Extension methods to register all Infrastructure services in the DI container.
/// </summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<SeafoodDbContext>(opts =>
            opts.UseMySql(
                configuration.GetConnectionString("DefaultConnection"),
                ServerVersion.AutoDetect(configuration.GetConnectionString("DefaultConnection")),
                mysql => mysql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddScoped<ISessionRepository, SessionRepository>();

        return services;
    }
}
