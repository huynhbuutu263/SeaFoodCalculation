using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Inspection.Pipeline;
using SeafoodVision.Presentation.Helpers;
using SeafoodVision.Presentation.Views;

namespace SeafoodVision.Presentation.ViewModels;

/// <summary>
/// Master orchestration ViewModel for the Recipe Editor UI.
/// Manages CRUD operations for InspectionRecipes via IRecipeRepository
/// and launches child dialogs for defining ROIs and Vision Chains.
/// </summary>
public sealed class RecipeEditorViewModel : ViewModelBase, IDisposable
{
    private readonly IRecipeRepository _repository;
    private readonly RoiPipelineRunner _pipelineRunner;

    // We keep these separate so we only commit to DB when user clicks "Save"
    public ObservableCollection<InspectionRecipe> Recipes { get; } = new();

    private InspectionRecipe? _selectedRecipe;
    public InspectionRecipe? SelectedRecipe
    {
        get => _selectedRecipe;
        set
        {
            if (SetField(ref _selectedRecipe, value))
            {
                RaiseCanExecuteChangedEvents();
                // Ensure UI reflects the active ROIs of the selected recipe
                Rois.Clear();
                if (_selectedRecipe != null)
                {
                    foreach (var r in _selectedRecipe.RoiDefinitions.OrderBy(x => x.Order))
                        Rois.Add(r);
                }
                SelectedRoi = Rois.FirstOrDefault();
            }
        }
    }

    public ObservableCollection<RoiDefinition> Rois { get; } = new();

    private RoiDefinition? _selectedRoi;
    public RoiDefinition? SelectedRoi
    {
        get => _selectedRoi;
        set
        {
            if (SetField(ref _selectedRoi, value))
                RaiseCanExecuteChangedEvents();
        }
    }

    public IEnumerable<ExecutionMode> AvailableModes => Enum.GetValues<ExecutionMode>();

    // ── Reference Material ────────────────────────────────────────────────────

    private string _referenceImagePath = string.Empty;
    public string ReferenceImagePath
    {
        get => _referenceImagePath;
        private set => SetField(ref _referenceImagePath, value);
    }

    private Mat? _referenceMat;
    private BitmapSource? _referenceBitmap;
    public BitmapSource? ReferenceBitmap
    {
        get => _referenceBitmap;
        private set => SetField(ref _referenceBitmap, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand LoadReferenceImageCommand { get; }
    public ICommand AddRecipeCommand { get; }
    public ICommand DeleteRecipeCommand { get; }
    public ICommand SaveChangesCommand { get; }
    
    public ICommand AddRoiCommand { get; }
    public ICommand DeleteRoiCommand { get; }
    
    // Child Dialog Launchers
    public ICommand DrawRoiRegionCommand { get; }
    public ICommand ConfigureVisionStepsCommand { get; }

    public RecipeEditorViewModel(IRecipeRepository repository, RoiPipelineRunner pipelineRunner)
    {
        _repository = repository;
        _pipelineRunner = pipelineRunner;

        LoadReferenceImageCommand = new RelayCommand(OnLoadReferenceImage);
        AddRecipeCommand = new RelayCommand(OnAddRecipe);
        DeleteRecipeCommand = new AsyncRelayCommand(OnDeleteRecipe, () => SelectedRecipe != null);
        SaveChangesCommand = new AsyncRelayCommand(OnSaveChanges);

        AddRoiCommand = new RelayCommand(OnAddRoi, () => SelectedRecipe != null);
        DeleteRoiCommand = new RelayCommand(OnDeleteRoi, () => SelectedRoi != null);

        DrawRoiRegionCommand = new RelayCommand(OnDrawRoiRegion, () => SelectedRoi != null && _referenceMat != null);
        ConfigureVisionStepsCommand = new RelayCommand(OnConfigureVisionSteps, () => SelectedRoi != null && _referenceMat != null);
    }

    public async Task InitializeAsync()
    {
        var dbRecipes = await _repository.GetAllAsync();
        Recipes.Clear();
        foreach (var r in dbRecipes)
            Recipes.Add(r);

        SelectedRecipe = Recipes.FirstOrDefault();
    }

    private void RaiseCanExecuteChangedEvents()
    {
        (DeleteRecipeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        ((RelayCommand)AddRoiCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DeleteRoiCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DrawRoiRegionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ConfigureVisionStepsCommand).RaiseCanExecuteChanged();
    }

    // ── Command Handlers ──────────────────────────────────────────────────────

    private void OnLoadReferenceImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
            Title = "Load Reference Frame"
        };

        if (dialog.ShowDialog() == true)
        {
            DisposeReferenceImage();
            ReferenceImagePath = dialog.FileName;
            _referenceMat = Cv2.ImRead(dialog.FileName, ImreadModes.Color);
            
            if (!_referenceMat.Empty())
            {
                ReferenceBitmap = _referenceMat.ToBitmapSource();
                RaiseCanExecuteChangedEvents();
            }
        }
    }

    private void OnAddRecipe()
    {
        var r = InspectionRecipe.Create("New Recipe", "CAM-01");
        Recipes.Add(r);
        SelectedRecipe = r;
    }

    private async Task OnDeleteRecipe()
    {
        if (SelectedRecipe == null) return;
        await _repository.DeleteAsync(SelectedRecipe.Id);
        Recipes.Remove(SelectedRecipe);
        SelectedRecipe = Recipes.FirstOrDefault();
    }

    private async Task OnSaveChanges()
    {
        foreach (var r in Recipes)
        {
            if (r.Id == Guid.Empty)
                await _repository.AddAsync(r);
            else
                await _repository.UpdateAsync(r);
        }
        
        // Let's pretend we have a MessageBox or Status indicator...
    }

    private void OnAddRoi()
    {
        if (SelectedRecipe == null) return;
        var r = SelectedRecipe.AddRoi("New Region", SeafoodVision.Domain.ValueObjects.RegionOfInterest.FromRectangle(new(0f, 0f), new(1f, 1f)));
        Rois.Add(r);
        SelectedRoi = r;
    }

    private void OnDeleteRoi()
    {
        if (SelectedRecipe == null || SelectedRoi == null) return;
        SelectedRecipe.RemoveRoi(SelectedRoi.Id);
        Rois.Remove(SelectedRoi);
        SelectedRoi = Rois.FirstOrDefault();
    }

    // ── Dialog Launchers ──────────────────────────────────────────────────────

    private void OnDrawRoiRegion()
    {
        if (SelectedRoi == null || _referenceBitmap == null) return;

        var vm = new RoiDrawingViewModel
        {
            RoiName = SelectedRoi.Name,
            ActiveShapeType = SelectedRoi.Region.RegionType,
            Region = SelectedRoi.Region,
            ReferenceFrame = _referenceBitmap
        };

        var dialog = new RoiDrawingDialog(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && vm.IsConfirmed && vm.Region != null)
        {
            // Dialog confirmed, apply changes
            SelectedRoi.Rename(vm.RoiName);
            SelectedRoi.UpdateRegion(vm.Region);
            
            // Re-trigger property changed to force UI bindings update
            OnPropertyChanged(nameof(SelectedRoi));
        }
    }

    private void OnConfigureVisionSteps()
    {
        if (SelectedRoi == null || _referenceMat == null) return;

        var vm = new VisionConfigViewModel(SelectedRoi, _referenceMat, _pipelineRunner);
        var dialog = new VisionConfigDialog(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        // Changes map directly to Domain object step list internally inside VisionConfigViewModel
        dialog.ShowDialog();
    }

    private void DisposeReferenceImage()
    {
        _referenceMat?.Dispose();
        _referenceMat = null;
        ReferenceBitmap = null;
    }

    public void Dispose()
    {
        DisposeReferenceImage();
    }
}
