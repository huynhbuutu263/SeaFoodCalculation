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

src/SeafoodVision.Infrastructure/
  Data/Repositories/
    RecipeRepository.cs        ← MODIFIED: UpdateAsync now uses load+diff strategy for correct child sync
```

---

## 2. Core Concepts & Architecture

The **Recipe Editor Dialog** is the main entry point for configuring the inspection logic. It integrates domain aggregate roots (`InspectionRecipe` and `RoiDefinition`) with the physical camera feeds (via loading static reference images) and provides a gateway to the editors from previous phases.

### 2.1 Database Integration (EF Core via Repository)
- Upon the UI `Window_Loaded` event, `RecipeEditorViewModel.InitializeAsync()` is fired, which yields all available Recipes from `IRecipeRepository.GetAllAsync()`.
- The ViewModel tracks `_existingIds` (a `HashSet<Guid>`) to know which recipes came from the database. New in-memory recipes are NOT in this set.
- Updates (like adding an ROI, or tweaking properties) sit in memory.
- The user must explicitly hit **"Save All To Database"**, which fires `SaveChangesCommand`. This sweeps through the observable collection and pushes inserts/updates via the asynchronous repository layer, then calls `SaveChangesAsync()` to commit.
- After a successful save, `_existingIds` is updated to include newly saved recipes.

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

### 3.3 New vs Existing Recipe Detection
- **Issue**: `InspectionRecipe.Create()` always assigns a new `Guid`, so the old `if (r.Id == Guid.Empty)` check was never true — new recipes would go through `UpdateAsync` and fail silently.
- **Fix**: The ViewModel maintains a `_existingIds` (`HashSet<Guid>`) populated from the DB during `InitializeAsync()`. `OnSaveChanges` uses `_existingIds.Contains(r.Id)` to route each recipe to `AddAsync` or `UpdateAsync` correctly.

### 3.4 Missing SaveChangesAsync Calls
- **Issue**: `OnSaveChanges` and `OnDeleteRecipe` called `AddAsync`/`UpdateAsync`/`DeleteAsync` but never called `SaveChangesAsync()`, so no data was ever persisted to the database.
- **Fix**: Both `OnSaveChanges` and `OnDeleteRecipe` now call `_repository.SaveChangesAsync()` after staging all operations.

### 3.5 UpdateAsync Child Entity Synchronisation
- **Issue**: The original `UpdateAsync` called `_context.InspectionRecipes.Update(entity)` on a disconnected entity. EF Core marks all children as `Modified`, so **new ROIs and Steps were never inserted** and **deleted ROIs were never removed**.
- **Fix**: `RecipeRepository.UpdateAsync` now loads the existing record from the DB (with tracking), then diffs the incoming graph:
  - New ROIs → added via `_context.RoiDefinitions.Add()`
  - Removed ROIs → deleted via `_context.RoiDefinitions.Remove()` (cascade removes their Steps)
  - Modified ROIs → scalar properties updated via `Entry.CurrentValues.SetValues()`
  - Same logic recursively applied to Steps within each ROI

### 3.6 Status Feedback
- **Issue**: The original `OnSaveChanges` ended with a comment: `// Let's pretend we have a MessageBox or Status indicator...` — there was no user feedback.
- **Fix**: Added a `StatusMessage` string property to `RecipeEditorViewModel` and bound it in the toolbar area of `RecipeEditorDialog.xaml`. The message updates on add, delete, save success, and save failure.

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

**Q: "Save All To Database" reports success but data is not in MySQL.**
- Verify `SaveChangesAsync()` is being reached (check for exceptions in `StatusMessage`).
- Check MySQL connection string in `appsettings.json`.

**Q: New ROIs added to an existing recipe are not saved.**
- Confirmed fixed in this phase. `RecipeRepository.UpdateAsync` now loads tracked data from DB and diffs.
- If still failing: check that `_existingIds` in the ViewModel is correctly seeded from `InitializeAsync`.

**Q: "Save All To Database" throws an Entity tracking error.**
- EF's change tracker can conflict when the same entity is loaded twice. Always make sure `RecipeEditorViewModel.InitializeAsync()` is called once at startup and the ViewModel is not shared across windows.

**Q: Memory leaks when drawing or editing steps repeatedly.**
- Monitor process overhead. The `RecipeEditorViewModel` holds the `_referenceMat` (unmanaged OpenCV). Ensure the dialog's `Closed` event triggers `viewModel.Dispose()`.
