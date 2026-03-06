using Microsoft.Extensions.Logging;
using SeafoodVision.Application.DTOs;
using SeafoodVision.Application.Interfaces;
using SeafoodVision.Presentation.Models;
using SeafoodVision.Presentation.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Inspection.Pipeline;
using SeafoodVision.Presentation.Views;

namespace SeafoodVision.Presentation.ViewModels;

/// <summary>
/// ViewModel for the main dashboard window.
/// Binds to <see cref="ICountingOrchestrator"/> without any business logic in this layer.
/// Implements <see cref="INotifyPropertyChanged"/> directly — no third-party toolkit required.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly ICountingOrchestrator _orchestrator;
    private readonly IFrameVisualizationService _frameVisualization;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;

    private int _currentCount;
    private string _statusMessage = "Ready";
    private bool _isRunning;
    private CountingSessionDto? _currentSession;
    private BitmapSource? _videoFrame;
    private bool _isDisplayVideoEnabled;

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

    public BitmapSource? VideoFrame
    {
        get => _videoFrame;
        private set => SetField(ref _videoFrame, value);
    }

    public ObservableCollection<DetectionOverlay> Overlays { get; } = new();

    public bool IsDisplayVideoEnabled
    {
        get => _isDisplayVideoEnabled;
        set => SetField(ref _isDisplayVideoEnabled, value);
    }

    public ICommand StartCommand => _startCommand;
    public ICommand StopCommand => _stopCommand;
    public ICommand OpenRecipeEditorCommand { get; }

    private readonly AsyncRelayCommand _startCommand;
    private readonly AsyncRelayCommand _stopCommand;

    public MainViewModel(
        ICountingOrchestrator orchestrator,
        IFrameVisualizationService frameVisualization,
        ILogger<MainViewModel> logger,
        IServiceProvider serviceProvider)
    {
        _orchestrator = orchestrator;
        _frameVisualization = frameVisualization;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _orchestrator.CountUpdated += OnCountUpdated;
        _orchestrator.FrameVisualUpdated += OnFrameVisualUpdated;

        _startCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning);
        _stopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning);
        OpenRecipeEditorCommand = new RelayCommand(OnOpenRecipeEditor);
    }

    private void OnOpenRecipeEditor()
    {
        var recipeRepo = _serviceProvider.GetRequiredService<IRecipeRepository>();
        var runner = _serviceProvider.GetRequiredService<RoiPipelineRunner>();

        var editorVm = new RecipeEditorViewModel(recipeRepo, runner);
        var dialog = new RecipeEditorDialog(editorVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        dialog.ShowDialog();
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

    private void OnFrameVisualUpdated(object? sender, FrameVisualDto e)
    {
        if (!IsDisplayVideoEnabled)
            return;

        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var (frame, overlays) = _frameVisualization.CreateVisuals(e);
                if (frame is not null)
                {
                    VideoFrame = frame;
                }

                Overlays.Clear();
                foreach (var overlay in overlays)
                {
                    Overlays.Add(overlay);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update video frame for UI.");
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        _orchestrator.CountUpdated -= OnCountUpdated;
        _orchestrator.FrameVisualUpdated -= OnFrameVisualUpdated;
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
