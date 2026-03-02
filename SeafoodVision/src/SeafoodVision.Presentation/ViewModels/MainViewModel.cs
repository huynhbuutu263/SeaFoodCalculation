using Microsoft.Extensions.Logging;
using SeafoodVision.Application.DTOs;
using SeafoodVision.Application.Interfaces;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace SeafoodVision.Presentation.ViewModels;

/// <summary>
/// ViewModel for the main dashboard window.
/// Binds to <see cref="ICountingOrchestrator"/> without any business logic in this layer.
/// Implements <see cref="INotifyPropertyChanged"/> directly — no third-party toolkit required.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly ICountingOrchestrator _orchestrator;
    private readonly ILogger<MainViewModel> _logger;

    private int _currentCount;
    private string _statusMessage = "Ready";
    private bool _isRunning;
    private CountingSessionDto? _currentSession;

    public int CurrentCount
    {
        get => _currentCount;
        private set => SetField(ref _currentCount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetField(ref _isRunning, value))
            {
                _startCommand.RaiseCanExecuteChanged();
                _stopCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public CountingSessionDto? CurrentSession
    {
        get => _currentSession;
        private set => SetField(ref _currentSession, value);
    }

    public ICommand StartCommand => _startCommand;
    public ICommand StopCommand => _stopCommand;

    private readonly AsyncRelayCommand _startCommand;
    private readonly AsyncRelayCommand _stopCommand;

    public MainViewModel(ICountingOrchestrator orchestrator, ILogger<MainViewModel> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _orchestrator.CountUpdated += OnCountUpdated;

        _startCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning);
        _stopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning);
    }

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

    // ── INotifyPropertyChanged ──────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Sets <paramref name="field"/> to <paramref name="value"/> and raises
    /// <see cref="PropertyChanged"/> when the value actually changes.
    /// </summary>
    /// <returns><c>true</c> if the value changed; otherwise <c>false</c>.</returns>
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
