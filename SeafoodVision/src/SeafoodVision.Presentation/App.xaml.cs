using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SeafoodVision.AI;
using SeafoodVision.Application.Interfaces;
using SeafoodVision.Application.Services;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Hardware;
using SeafoodVision.Infrastructure;
using SeafoodVision.Presentation.ViewModels;
using SeafoodVision.Presentation.Views;
using System.Windows;

namespace SeafoodVision.Presentation;

/// <summary>  
/// Composition root: configures the DI container and launches the main window.  
/// </summary>  
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        _serviceProvider = services.BuildServiceProvider();

        // Ensure the database and all tables exist before the UI starts.
        // If the DB or tables are missing they will be created automatically.
        _serviceProvider.EnsureDbCreatedAsync().GetAwaiter().GetResult();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        services.AddInfrastructure(configuration);
        services.AddHardware(configuration);
        services.AddAIServices(configuration);

        services.AddScoped<ITrackingService, TrackingService>();
        services.AddScoped<ICountingService, CountingService>();
        services.AddScoped<ICountingOrchestrator, CountingOrchestrator>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}