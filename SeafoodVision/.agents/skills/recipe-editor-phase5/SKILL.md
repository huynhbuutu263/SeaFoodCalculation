---
name: inspection-recipe-editor-phase5
description: >
  Reference guide for Phase 5 of the Label Teaching Tool implementation.
  Covers the Main Recipe Editor UI (RecipeEditorDialog), orchestrating
  the selection of Recipes, their ROIs, and launching child dialogs
  for ROI Drawing (Phase 3) and Pipeline Configuration (Phase 4).
---

# Phase 5 — Main Recipe Editor Dialog Orchestration

## 1. What Was Added

### New files at a glance

```
src/SeafoodVision.Presentation/
  ViewModels/
    RelayCommand.cs            ← MODIFIED: Supports AsyncRelayCommand for safer async/await
    RecipeEditorViewModel.cs   ← Central Hub: Manages Recipes, ROIs, Reference images, and Dialog triggers
  Views/
    RecipeEditorDialog.xaml    ← Master UI combining Recipe List and ROI list with action buttons
    RecipeEditorDialog.xaml.cs ← Code-behind that initializes the DB load
```

---

## 2. Core Concepts & Architecture

The **Recipe Editor Dialog** is the main entry point for configuring the inspection logic. It integrates domain aggregate roots (`InspectionRecipe` and `RoiDefinition`) with the physical camera feeds (via loading static reference images) and provides a gateway to the editors from previous phases.

### 2.1 Database Integration (EF Core via Repository)
- Upon the UI `Window_Loaded` event, `RecipeEditorViewModel.InitializeAsync()` is fired, which yields all available Recipes from `IRecipeRepository.GetAllAsync()`.
- Updates (like adding an ROI, or tweaking properties) sit in memory. 
- The user must explicitly hit **"Save All To Database"**, which fires `SaveChangesCommand`. This sweeps through the observable collection and pushes inserts/updates via the asynchronous repository layer.

### 2.2 Reference Image Dependency
- Launching the Drawing Dialog (Phase 3) or Vision Step Pipeline Editor (Phase 4) **requires** a backing image (`Mat`) to render over. 
- You cannot launch these sub-dialogs unless the user clicks **Browse Image** and selects a reference frame. 
- The ViewModel handles cleanup by enforcing `System.IDisposable` and securely calling `.Dispose()` on the unmanaged OpenCV `Mat` if a new frame is loaded or the window is closed.

### 2.3 Child Dialog Spawning Mechanisms
When you select an ROI and click either:
- **Draw ROI Coordinates**: `OnDrawRoiRegion` is invoked. It creates a Phase 3 `RoiDrawingViewModel`, loads the `ReferenceBitmap`, opens it as a modal (`ShowDialog()`), and if confirmed, pushes the coordinates back into `SelectedRoi.UpdateRegion()`.
- **Edit Vision Pipeline**: `OnConfigureVisionSteps` is invoked. It hands off the native unmanaged OpenCV `ReferenceMat` and the `RoiDefinition` to Phase 4's `VisionConfigViewModel`. Any pipeline modifications inside that dialog are committed live to the ROI aggregate.

---

## 3. Key Quirks & Solutions Implemented

### 3.1 Synchronous vs Asynchronous ICommand Signatures
- **Issue**: Attempting to hook `await _repository.DeleteAsync()` to a standard `RelayCommand` raised issues because properties required an `Action`, not a `Func<Task>`. Doing `async void` inside command delegates is dangerous.
- **Fix**: Upgraded the `RelayCommand.cs` file to support an `AsyncRelayCommand` wrapper that safely handles `Func<Task>` execution, manages an internal `_isExecuting` semaphore, and suppresses background exceptions from crashing the thread pool context.

### 3.2 Dynamic List Updating
- **Issue**: XAML ListBoxes can get disconnected if you swap out the root ObservableCollection.
- **Fix**: Operations inside the ViewModel intentionally call `.Clear()` and `.Add()` items manually within `InitializeAsync` rather than re-instantiating `new ObservableCollection` variables, preserving WPF UI bindings.

---

## 4. How to Invoke the Dialog from the Main App

In order to wire this directly into your application (Phase 6), you will inject the Repository inside the Main Window or Main ViewModel, then spawn it:

```csharp
// Inside MainViewModel.cs or similar orchestrator

public ICommand OpenRecipeEditorCommand { get; }

private void OnOpenRecipeEditor()
{
    // These should ideally be obtained via Dependency Injection inside App.xaml.cs
    var recipeRepo = _serviceProvider.GetRequiredService<IRecipeRepository>();
    var runner = _serviceProvider.GetRequiredService<RoiPipelineRunner>();

    var editorVm = new RecipeEditorViewModel(recipeRepo, runner);
    var dialog = new RecipeEditorDialog(editorVm)
    {
        Owner = System.Windows.Application.Current.MainWindow
    };

    dialog.ShowDialog();
}
```

---

## 5. Quick Debug Checklist

**Q: "Save All To Database" command drops properties or throws Entity errors.**
- If you've modified how `RoiDefinition` or `InspectionStep` relates to EF Core internally, verify the navigation property mappings inside `AppDbContext` haven't broken cascades.

**Q: Memory leaks when drawing or editing steps repeatedly.**
- Monitor process overhead. Wait, why would it leak? Because `Mat` is unmanaged. The `RecipeEditorViewModel` holds the `_referenceMat`. Ensure the dialog's `Closed` event is triggering `viewModel.Dispose()`.
