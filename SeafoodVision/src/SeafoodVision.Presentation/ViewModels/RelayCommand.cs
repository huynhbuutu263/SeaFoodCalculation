using System.Windows.Input;

namespace SeafoodVision.Presentation.ViewModels;

/// <summary>
/// A synchronous <see cref="ICommand"/> implementation that wraps an
/// <see cref="Action"/> and an optional <c>canExecute</c> predicate.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _execute();

    /// <summary>Raises <see cref="CanExecuteChanged"/> so bound controls re-query.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// An asynchronous <see cref="ICommand"/> implementation that wraps a
/// <see cref="Func{Task}"/> and an optional <c>canExecute</c> predicate.
/// Prevents re-entrancy while the previous invocation is still running.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) =>
        !_isExecuting && (_canExecute?.Invoke() ?? true);

    /// <inheritdoc/>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Prevent unhandled exceptions from crashing the application
            // via the async-void fire-and-forget path.
            System.Diagnostics.Debug.WriteLine($"[AsyncRelayCommand] Unhandled exception: {ex}");
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>Raises <see cref="CanExecuteChanged"/> so bound controls re-query.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
