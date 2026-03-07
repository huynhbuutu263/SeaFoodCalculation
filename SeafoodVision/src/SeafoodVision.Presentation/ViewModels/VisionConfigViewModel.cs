using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Inspection.Pipeline;
using SeafoodVision.Presentation.Helpers;

namespace SeafoodVision.Presentation.ViewModels;

/// <summary>
/// Orchestrates the Vision Parameter Configuration Dialog.
/// Allows users to build, reorder, and live-preview a chain of <see cref="InspectionStep"/>s
/// belonging to a single <see cref="RoiDefinition"/>.
/// </summary>
public sealed class VisionConfigViewModel : ViewModelBase, IDisposable
{
    private readonly RoiDefinition _roi;
    private readonly Mat _fullFrame;
    private readonly RoiPipelineRunner _pipelineRunner;

    public ObservableCollection<VisionStepViewModel> Steps { get; } = new();

    private VisionStepViewModel? _selectedStep;
    public VisionStepViewModel? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (SetField(ref _selectedStep, value))
            {
                // Unhook previous if any
                HookSelectedStepPropertyChanges(oldStep: true);
                
                // Re-calculate the preview frame when selection changes
                HookSelectedStepPropertyChanges(oldStep: false);
                UpdatePreview();

                // Refresh input-source picker
                OnPropertyChanged(nameof(AvailableInputSources));
                SyncSelectedInputSource();

                ((RelayCommand)RemoveCommand).RaiseCanExecuteChanged();
                ((RelayCommand)MoveUpCommand).RaiseCanExecuteChanged();
                ((RelayCommand)MoveDownCommand).RaiseCanExecuteChanged();
            }
        }
    }

    private BitmapSource? _previewFrame;
    public BitmapSource? PreviewFrame
    {
        get => _previewFrame;
        private set => SetField(ref _previewFrame, value);
    }
    
    // Command properties
    public ICommand AddStepCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    
    public Action? CloseDialogAction { get; set; }

    public VisionConfigViewModel(RoiDefinition roi, Mat fullFrame, RoiPipelineRunner pipelineRunner)
    {
        _roi = roi ?? throw new ArgumentNullException(nameof(roi));
        _fullFrame = fullFrame ?? throw new ArgumentNullException(nameof(fullFrame));
        _pipelineRunner = pipelineRunner ?? throw new ArgumentNullException(nameof(pipelineRunner));
        
        // Load initial steps into ObservableCollection
        foreach (var step in _roi.Steps.OrderBy(s => s.Order))
        {
            Steps.Add(new VisionStepViewModel(step));
        }

        AddStepCommand = new RelayCommand(OnAddStep);
        RemoveCommand = new RelayCommand(OnRemoveSelected, () => SelectedStep is not null);
        MoveUpCommand = new RelayCommand(OnMoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(OnMoveDown, CanMoveDown);

        UpdatePreview(); // Show raw ROI initially if zero steps
    }

    private void HookSelectedStepPropertyChanges(bool oldStep)
    {
        // Whenever a user tweaks a slider/textbox on the active selected Step's Parameters, 
        // we bounce the update back to the JSON string and trigger a Pipeline Preview run.
        if (SelectedStep?.Parameters is INotifyPropertyChanged notifyObj)
        {
            if (oldStep)
                notifyObj.PropertyChanged -= OnSelectedStepParamChanged;
            else
                notifyObj.PropertyChanged += OnSelectedStepParamChanged;
        }
    }

    private void OnSelectedStepParamChanged(object? sender, PropertyChangedEventArgs e)
    {
        SelectedStep?.SaveParametersToModel();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        // Find how many steps deep we need to run in the pipeline (up to the Selected Step inclusively)
        int stepLimit = SelectedStep != null 
            ? Steps.IndexOf(SelectedStep) + 1 
            : Steps.Count; 
            
        // Default scenario: No steps or no selection just show the cropped ROI base frame
        if (Steps.Count == 0) stepLimit = 0;

        // Run the pipeline and get the newly generated mat
        using var result = _pipelineRunner.Run(_fullFrame, _roi, stepLimit);
        
        if (result.ResultMat != null && !result.ResultMat.IsDisposed)
        {
            PreviewFrame = result.ResultMat.ToBitmapSource();
        }
        else
        {
            PreviewFrame = null;
        }
    }

    // Properties for selecting which StepType to add
    public IEnumerable<StepType> AvailableStepTypes => Enum.GetValues<StepType>();
    
    private StepType _selectedNewStepType = StepType.Threshold;
    public StepType SelectedNewStepType
    {
        get => _selectedNewStepType;
        set => SetField(ref _selectedNewStepType, value);
    }

    /// <summary>
    /// Returns a list of step-order/name pairs that the currently selected step can reference as
    /// a primary input (all steps that come before it in order).
    /// The first item is always <c>null</c> representing the default sequential input.
    /// </summary>
    public IEnumerable<InputSourceItem> AvailableInputSources
    {
        get
        {
            yield return new InputSourceItem(null, "(Previous step – default)");
            if (SelectedStep == null) yield break;
            int selectedOrder = SelectedStep.Model.Order;
            foreach (var step in Steps.Where(s => s.Model.Order < selectedOrder))
                yield return new InputSourceItem(step.Model.Order, $"Step {step.Model.Order}: {step.Name}");
        }
    }

    private InputSourceItem? _selectedInputSource;
    public InputSourceItem? SelectedInputSource
    {
        get => _selectedInputSource;
        set
        {
            if (SetField(ref _selectedInputSource, value) && SelectedStep != null)
            {
                SelectedStep.InputStepOrder = value?.Order;
                UpdatePreview();
            }
        }
    }

    #region Add / Remove / Reorder Logic

    private void OnAddStep()
    {
        var newStep = _roi.AddStep(SelectedNewStepType);
        
        var vm = new VisionStepViewModel(newStep);
        Steps.Add(vm);
        SelectedStep = vm;
        
        SyncOrderIndexes(); // Ensure contiguous 1-based indexes for EF Core
    }

    private void OnRemoveSelected()
    {
        if (SelectedStep == null) return;
        
        _roi.RemoveStep(SelectedStep.Model.Id);
        Steps.Remove(SelectedStep);
        
        SelectedStep = Steps.LastOrDefault();
        SyncOrderIndexes();
    }

    private bool CanMoveUp() => SelectedStep != null && Steps.IndexOf(SelectedStep) > 0;
    private void OnMoveUp()
    {
        if (SelectedStep == null) return;
        int idx = Steps.IndexOf(SelectedStep);
        if (idx > 0)
        {
            Steps.Move(idx, idx - 1);
            SyncOrderIndexes();
            UpdatePreview(); // Context shifted, re-evaluate output
        }
    }

    private bool CanMoveDown() => SelectedStep != null && Steps.IndexOf(SelectedStep) < Steps.Count - 1;
    private void OnMoveDown()
    {
        if (SelectedStep == null) return;
        int idx = Steps.IndexOf(SelectedStep);
        if (idx < Steps.Count - 1)
        {
            Steps.Move(idx, idx + 1);
            SyncOrderIndexes();
            UpdatePreview();
        }
    }

    private void SyncOrderIndexes()
    {
        // Re-assign Orders.
        // It's critical for the EF Core configuration if we don't want cascading overlaps list issues.
        for (int i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            step.Model.UpdateOrder(i + 1);
        }
        
        ((RelayCommand)MoveUpCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MoveDownCommand).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(AvailableInputSources));
    }

    private void SyncSelectedInputSource()
    {
        if (SelectedStep == null)
        {
            _selectedInputSource = null;
            OnPropertyChanged(nameof(SelectedInputSource));
            return;
        }

        var order = SelectedStep.InputStepOrder;
        _selectedInputSource = AvailableInputSources
            .FirstOrDefault(s => s.Order == order)
            ?? AvailableInputSources.First(); // default = "(Previous step)"
        OnPropertyChanged(nameof(SelectedInputSource));
    }

    #endregion

    public void Dispose()
    {
        // Ensure no lingering memory ties if the dialog is killed
        HookSelectedStepPropertyChanges(oldStep: true);
        PreviewFrame = null;
    }
}

/// <summary>
/// Represents a selectable input-source step in the Vision Config UI.
/// <c>Order = null</c> means "use the default sequential input".
/// </summary>
public sealed record InputSourceItem(int? Order, string DisplayName)
{
    public override string ToString() => DisplayName;
}
