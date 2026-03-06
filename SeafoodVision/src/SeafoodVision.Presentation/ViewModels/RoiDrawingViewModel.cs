using System.Windows.Input;
using System.Windows.Media.Imaging;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Presentation.ViewModels;

/// <summary>
/// ViewModel for the ROI Drawing Dialog. 
/// Handles binding to the RoiCanvas overlay and manages region state.
/// </summary>
public sealed class RoiDrawingViewModel : ViewModelBase
{
    private string _roiName = "New Zone";
    private RegionType _activeShapeType = RegionType.Rectangle;
    private RegionOfInterest? _region;
    private BitmapSource? _referenceFrame;
    private bool _isConfirmed;

    public string RoiName
    {
        get => _roiName;
        set => SetField(ref _roiName, value);
    }

    public RegionType ActiveShapeType
    {
        get => _activeShapeType;
        set
        {
            if (SetField(ref _activeShapeType, value))
            {
                // When switching shapes, we implicitly clear the existing drawing 
                // so the user can start fresh with the new tool.
                Region = null;
            }
        }
    }

    public RegionOfInterest? Region
    {
        get => _region;
        set => SetField(ref _region, value);
    }

    /// <summary>
    /// The static reference image the operator traces over.
    /// Needs to match the camera bounds exactly.
    /// </summary>
    public BitmapSource? ReferenceFrame
    {
        get => _referenceFrame;
        set => SetField(ref _referenceFrame, value);
    }

    /// <summary>True if the user pressed Confirm. False if Cancelled.</summary>
    public bool IsConfirmed
    {
        get => _isConfirmed;
        private set => SetField(ref _isConfirmed, value);
    }

    public ICommand ClearCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public Action? CloseDialogAction { get; set; }

    public RoiDrawingViewModel()
    {
        ClearCommand = new RelayCommand(OnClear);
        ConfirmCommand = new RelayCommand(OnConfirm, CanConfirm);
        CancelCommand = new RelayCommand(OnCancel);

        // Required so that RelayCommand re-evaluates CanConfirm when Region changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Region) && ConfirmCommand is RelayCommand rc)
                rc.RaiseCanExecuteChanged();
        };
    }

    private void OnClear() => Region = null;

    private void OnCancel()
    {
        IsConfirmed = false;
        CloseDialogAction?.Invoke();
    }

    private void OnConfirm()
    {
        IsConfirmed = true;
        CloseDialogAction?.Invoke();
    }

    // Must have drawn a valid region to confirm.
    private bool CanConfirm() => Region is not null;
}
