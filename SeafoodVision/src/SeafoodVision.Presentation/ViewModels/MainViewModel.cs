using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SeafoodVision.Application.DTOs;
using SeafoodVision.Application.Interfaces;

namespace SeafoodVision.Presentation.ViewModels;

/// <summary>
/// ViewModel for the main dashboard window.
/// Binds to <see cref="ICountingOrchestrator"/> without any business logic in this layer.
/// All properties are observable via CommunityToolkit.Mvvm source generators.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ICountingOrchestrator _orchestrator;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private int _currentCount;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private CountingSessionDto? _currentSession;

    public MainViewModel(ICountingOrchestrator orchestrator, ILogger<MainViewModel> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _orchestrator.CountUpdated += OnCountUpdated;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        try
        {
            IsRunning = true;
            StatusMessage = "Starting pipeline…";
            await _orchestrator.StartAsync("CAM-01");
            CurrentSession = _orchestrator.CurrentSession;
            StatusMessage = "Running";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start counting pipeline");
            StatusMessage = $"Error: {ex.Message}";
            IsRunning = false;
        }
    }

    private bool CanStart() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        try
        {
            StatusMessage = "Stopping pipeline…";
            await _orchestrator.StopAsync();
            CurrentSession = _orchestrator.CurrentSession;
            StatusMessage = $"Stopped. Total: {CurrentCount}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop counting pipeline");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsRunning = false; }
    }

    private bool CanStop() => IsRunning;

    private void OnCountUpdated(object? sender, int count)
    {
        // Marshal to UI thread
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            CurrentCount = count;
            CurrentSession = _orchestrator.CurrentSession;
        });
    }

    public async ValueTask DisposeAsync()
    {
        _orchestrator.CountUpdated -= OnCountUpdated;
        await _orchestrator.DisposeAsync();
    }
}
