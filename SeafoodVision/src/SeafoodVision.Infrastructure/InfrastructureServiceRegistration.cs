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
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing from configuration.");

        services.AddDbContext<SeafoodDbContext>(opts =>
            opts.UseMySql(
                connectionString,
                // Use a fixed known-good server version instead of AutoDetect,
                // because AutoDetect fails when the database does not exist yet.
                new MySqlServerVersion(new Version(8, 0, 0)),
                mysql => mysql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddScoped<ISessionRepository, SessionRepository>();

        return services;
    }

    /// <summary>
    /// Ensures the MySQL database and all tables exist.
    /// If the database or any table is missing it is created automatically using the
    /// EF Core model (equivalent to running 'dotnet ef database update').
    /// Call this once at application startup before serving any requests.
    /// </summary>
    public static async Task EnsureDbCreatedAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeafoodDbContext>();

        // EnsureCreatedAsync creates the database + all tables derived from the model
        // if they do not already exist.  It is a no-op when everything is already in place.
        await db.Database.EnsureCreatedAsync();
    }
}