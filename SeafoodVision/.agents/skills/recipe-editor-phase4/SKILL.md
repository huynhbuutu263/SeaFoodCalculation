---
name: inspection-recipe-editor-phase4
description: >
  Reference guide for Phase 4 of the Label Teaching Tool implementation.
  Covers the Vision Parameter Config Dialog, StepParameters observability,
  and real-time OpenCV pipeline preview mechanism.
  Use this to debug DataTemplates, INotifyPropertyChanged issues, 
  and the dynamic StepExecutor pipeline runner UI.
---

# Phase 4 — Vision Parameter Configuration UI

## 1. What Was Added

### New/Modified files at a glance

```
src/SeafoodVision.Inspection/
  Pipeline/Models/
    StepParameters.cs          ← MODIFIED: Inherits INotifyPropertyChanged (ParameterBase)
src/SeafoodVision.Presentation/
  Helpers/
    MatExtensions.cs           ← Helper to convert OpenCV Mat to WPF BitmapSource securely
  ViewModels/
    VisionStepViewModel.cs     ← Individual step wrapper handling JSON serialization
    VisionConfigViewModel.cs   ← Orchestrator for the sequence list and live preview Pipeline
  Views/
    VisionConfigDialog.xaml    ← The WPF parameter tuning window and live preview Map
    VisionConfigDialog.xaml.cs ← Code-behind
```

---

## 2. Core Concepts & Architecture

The Vision Parameter Config Dialog dynamically builds an inspection sequence (`InspectionStep`s) for a specific `RoiDefinition` and previews the results in real-time.

### 2.1 INotifyPropertyChanged in Domain/Infrastructure Models
Normally, Pipeline/Domain parameters are plain POCOs. However, because we want WPF Sliders to trigger **Live Previews** the instant they are dragged, we updated the `StepParameters.cs` models.
- **`ParameterBase`**: Centralizes `INotifyPropertyChanged` and the `SetField` method.
- **All Param Classes**: Changed from standard auto-properties to explicit backing fields using `SetField`. Default values are still respected.

### 2.2 Live Preview Loop
How does dragging a UI Slider automatically update the image?
1. The user drags a Slider bound to `ThresholdParams.ThreshValue`.
2. `ThreshValue` calls `SetField`, firing `PropertyChanged`.
3. `VisionConfigViewModel` subscribes to `SelectedStep.Parameters.PropertyChanged` exclusively for the active step.
4. When fired, it triggers `UpdatePreview()`.
5. `UpdatePreview()` invokes `_pipelineRunner.Run()` stopping exactly at the currently selected step index.
6. The resulting `Mat` is safely converted to a `BitmapSource` using `MatExtensions.ToBitmapSource()` and binds to `PreviewFrame`.

### 2.3 Polymorphic Parameter UI (DataTemplates)
Instead of a gigantic `Visibility` toggle mess, the UI utilizes WPF's `ContentControl` paired with `DataType` matching.
- **`VisionStepViewModel.Parameters`** stores the generic `object`.
- **`VisionConfigDialog.xaml`** defines `<DataTemplate DataType="{x:Type params:...}">` for each parameter class (e.g. `GaussianBlurParams`, `MorphologyParams`).
- WPF interrogates the runtime type inside the `ContentControl` and automatically inflates the correct `DataTemplate` (sliders/textboxes) matching that step type.

### 2.4 Entity Framework Core Sequence Preservation
EF Core requires ordered Collections (`Order` property) for `HasMany` relationships to persist properly.
- If steps are added, removed, or moved up/down, `SyncOrderIndexes()` is called.
- `SyncOrderIndexes()` invokes the domain method `_roi.ReorderStep()`, maintaining a strict contiguous 1-based index to prevent DbUpdate exceptions.

---

## 3. Key Quirks & Solutions Implemented

### 3.1 CS1729 & CS1061 Invocation Errors
- **Issue**: The UI logic attempted to call `new InspectionStep(...)` directly, bypassing the `RoiDefinition` aggregate root enforcement causing compilation failures.
- **Fix**: Re-routed calls through proper aggregate roots: `_roi.AddStep(SelectedNewStepType)`, `_roi.RemoveStep(SelectedStep.Model.Id)`.

### 3.2 Threading & Safe Memory Conversion (`MatExtensions.cs`)
- **Issue**: OpenCV `Mat` memory lives in unmanaged C++ land. WPF Bitmaps live in managed C# land. You cannot simply share the pointer because WPF needs to lock images for the Render thread (`InvalidOperationException`).
- **Fix**: `MatExtensions.ToBitmapSource()` handles this by utilizing `Cv2.ImEncode` to serialize the frame to an in-memory `.bmp` byte array, piping it into a `BitmapImage`, and crucially calling `bmp.Freeze()` to make it immutable and safely cross-thread accessible.

### 3.3 Dynamic StepType Selection
- **Issue**: The original mock relied on hardcoding `StepType.Threshold`.
- **Fix**: Implemented a `ComboBox` bound to `AvailableStepTypes = Enum.GetValues<StepType>()`, capturing the selection in `SelectedNewStepType`. 

---

## 4. How to Invoke the Dialog

When integrating this into the main window later (Phase 5), the dialog should be invoked like this on an ALREADY DRAWN `RoiDefinition`:

```csharp
var roi = ... // Retrieve from Domain Model
var fullFrame = ... // Get from Camera (must be original frame size!)
var pipelineRunner = ... // Injected via DI

var vm = new VisionConfigViewModel(roi, fullFrame, pipelineRunner);
var dialog = new VisionConfigDialog(vm)
{
    Owner = System.Windows.Application.Current.MainWindow
};

dialog.ShowDialog(); // Pipeline edits directly mutate the `roi` parameters.
```

---

## 5. Quick Debug Checklist

**Q: A step's parameters panel is completely blank.**
- We haven't built a `DataTemplate` for it yet in `VisionConfigDialog.xaml`. 
- Fix: Add `<DataTemplate DataType="{x:Type params:MissingStepParams}">` in the `Window.Resources` section.

**Q: Moving sliders doesn't update the image preview.**
- Make sure the specific property inside `StepParameters.cs` calls `SetField(ref _var, value);`. Auto-properties (`get; set;`) will not fire `PropertyChanged`.
- Also ensure the Domain JSON update is occurring: `SaveParametersToModel()` should be called behind the scenes by `OnSelectedStepParamChanged`.

**Q: "Collection was modified; enumeration operation may not execute." when moving steps**
- Ensure `ObservableCollection.Move()` is used, and then properly sync the native EF Core `Order` properties so `RoiDefinition` isn't confused.
