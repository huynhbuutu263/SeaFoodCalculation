using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Domain.ValueObjects;
using SeafoodVision.Inspection.Pipeline;
using SeafoodVision.Inspection.Pipeline.Models;
using SeafoodVision.Presentation.Helpers;
using SeafoodVision.Presentation.Views;

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
    private readonly IReadOnlyList<RoiDefinition> _allRois;

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
                ((RelayCommand)DrawTemplateRegionCommand).RaiseCanExecuteChanged();
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

    /// <summary>Opens a drawing dialog so the user can drag the TemplateMatchRegion rectangle on the input image.</summary>
    public ICommand DrawTemplateRegionCommand { get; }
    
    public Action? CloseDialogAction { get; set; }

    /// <summary>
    /// All ROI definitions in the parent recipe.  Exposed so that TemplateMatchRegion steps
    /// can reference an existing ROI as their template source.
    /// </summary>
    public IReadOnlyList<RoiDefinition> AllRois => _allRois;

    public VisionConfigViewModel(
        RoiDefinition roi,
        Mat fullFrame,
        RoiPipelineRunner pipelineRunner,
        IReadOnlyList<RoiDefinition>? allRois = null)
    {
        _roi = roi ?? throw new ArgumentNullException(nameof(roi));
        _fullFrame = fullFrame ?? throw new ArgumentNullException(nameof(fullFrame));
        _pipelineRunner = pipelineRunner ?? throw new ArgumentNullException(nameof(pipelineRunner));
        _allRois = allRois ?? Array.Empty<RoiDefinition>();
        
        // Load initial steps into ObservableCollection
        foreach (var step in _roi.Steps.OrderBy(s => s.Order))
        {
            Steps.Add(new VisionStepViewModel(step));
        }

        AddStepCommand = new RelayCommand(OnAddStep);
        RemoveCommand = new RelayCommand(OnRemoveSelected, () => SelectedStep is not null);
        MoveUpCommand = new RelayCommand(OnMoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(OnMoveDown, CanMoveDown);
        DrawTemplateRegionCommand = new RelayCommand(OnDrawTemplateRegion,
            () => SelectedStep?.Type == StepType.TemplateMatchRegion);

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
    /// Items whose output type is incompatible with the selected step's required input are
    /// flagged as <see cref="InputSourceItem.IsCompatible"/> = <c>false</c> (grayed out in the UI).
    /// </summary>
    public IEnumerable<InputSourceItem> AvailableInputSources
    {
        get
        {
            yield return new InputSourceItem(null, "(Previous step – default)", true);
            if (SelectedStep == null) yield break;

            int selectedOrder = SelectedStep.Model.Order;
            var requiredType = StepTypeInfo.RequiredInput(SelectedStep.Type);

            foreach (var step in Steps.Where(s => s.Model.Order < selectedOrder))
            {
                var outputType = StepTypeInfo.ProducedOutput(step.Type);
                bool compatible = StepTypeInfo.IsCompatible(outputType, requiredType);
                yield return new InputSourceItem(step.Model.Order, $"Step {step.Model.Order}: {step.Name}", compatible);
            }
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

    #region Draw Template Region

    /// <summary>
    /// Opens a drawing dialog over the input image of the selected TemplateMatchRegion step
    /// so the user can drag a rectangle instead of typing X/Y/W/H values.
    /// </summary>
    private void OnDrawTemplateRegion()
    {
        if (SelectedStep == null || SelectedStep.Type != StepType.TemplateMatchRegion) return;

        // Run the pipeline up to (but NOT including) the selected step to get its input image
        int stepIndex = Steps.IndexOf(SelectedStep);
        using var inputResult = _pipelineRunner.Run(_fullFrame, _roi, stepIndex);
        if (inputResult.ResultMat == null || inputResult.ResultMat.Empty()) return;

        var inputBitmap = inputResult.ResultMat.ToBitmapSource();
        if (inputBitmap == null) return;

        var drawVm = new RoiDrawingViewModel
        {
            ReferenceFrame = inputBitmap,
            ActiveShapeType = RegionType.Rectangle,
            RoiName = "Template Region"
        };

        // Pre-populate with the current template rectangle if already set
        if (SelectedStep.Parameters is TemplateMatchRegionParams existing
            && existing.TemplateWidth > 0 && existing.TemplateHeight > 0)
        {
            int fw = inputResult.ResultMat.Width;
            int fh = inputResult.ResultMat.Height;
            if (fw > 0 && fh > 0)
            {
                var tl = new System.Drawing.PointF((float)existing.TemplateX / fw, (float)existing.TemplateY / fh);
                var br = new System.Drawing.PointF((float)(existing.TemplateX + existing.TemplateWidth) / fw,
                                                   (float)(existing.TemplateY + existing.TemplateHeight) / fh);
                drawVm.Region = RegionOfInterest.FromRectangle(tl, br);
            }
        }

        var dialog = new RoiDrawingDialog(drawVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && drawVm.IsConfirmed && drawVm.Region != null)
        {
            var pixelRect = drawVm.Region.ToPixelRect(
                inputResult.ResultMat.Width, inputResult.ResultMat.Height);

            if (SelectedStep.Parameters is TemplateMatchRegionParams p)
            {
                p.RoiSourceName = string.Empty; // manual draw clears any ROI reference
                p.TemplateX = pixelRect.X;
                p.TemplateY = pixelRect.Y;
                p.TemplateWidth  = pixelRect.Width;
                p.TemplateHeight = pixelRect.Height;
                SelectedStep.SaveParametersToModel();
                UpdatePreview();
            }
        }
    }

    /// <summary>
    /// Applies the bounds of a recipe ROI as the template region for the selected
    /// TemplateMatchRegion step.  The ROI coordinates are translated from full-frame
    /// normalised space into pixel offsets relative to the current ROI's crop.
    /// </summary>
    public void ApplyRoiAsTemplateRegion(RoiDefinition sourceRoi)
    {
        if (SelectedStep?.Type != StepType.TemplateMatchRegion) return;
        if (SelectedStep.Parameters is not TemplateMatchRegionParams p) return;

        // Compute the current ROI crop offset in full-frame pixel space
        var currentCrop = _roi.Region.ToPixelRect(_fullFrame.Width, _fullFrame.Height);
        // Compute the selected ROI bounds in full-frame pixel space
        var sourceRect  = sourceRoi.Region.ToPixelRect(_fullFrame.Width, _fullFrame.Height);

        // Translate to be relative to the current crop
        p.RoiSourceName  = sourceRoi.Name;
        p.TemplateX      = sourceRect.X - currentCrop.X;
        p.TemplateY      = sourceRect.Y - currentCrop.Y;
        p.TemplateWidth  = sourceRect.Width;
        p.TemplateHeight = sourceRect.Height;

        SelectedStep.SaveParametersToModel();
        UpdatePreview();
    }

    #endregion

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
/// <see cref="IsCompatible"/> is <c>false</c> when the step's output type does not match
/// the required input type of the currently selected step — these items are grayed out in the UI.
/// </summary>
public sealed record InputSourceItem(int? Order, string DisplayName, bool IsCompatible = true)
{
    public override string ToString() => DisplayName;
}

