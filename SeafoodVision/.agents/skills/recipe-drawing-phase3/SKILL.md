---
name: inspection-recipe-drawing-phase3
description: >
  Reference guide for Phase 3 of the Label Teaching Tool implementation.
  Covers the ROI Drawing Dialog (WPF UI), ViewModel interactions, and
  the custom normalized coordinate Canvas (`RoiCanvas`).
  Use this to debug ROI drawing, shape modes (Rectangle vs Polygon),
  and Data Binding issues in WPF.
---

# Phase 3 — ROI Drawing Dialog UI

## 1. What Was Added

### New files at a glance

```
src/SeafoodVision.Presentation/
  Controls/
    RoiCanvas.cs               ← Custom WPF Canvas for interactive shape drawing
  Converters/
    EnumToBooleanConverter.cs  ← Converts RegionType enum for RadioButton bindings
  ViewModels/
    ViewModelBase.cs           ← Base class for INotifyPropertyChanged
    RelayCommand.cs            ← (Pre-existing/added) ICommand implementation
    RoiDrawingViewModel.cs     ← Manages state/actions for the drawing dialog
  Views/
    RoiDrawingDialog.xaml      ← The WPF dialog containing the Image + RoiCanvas overlay
    RoiDrawingDialog.xaml.cs   ← Code-behind wiring ViewModel Close actions
```

---

## 2. Core Concepts & Architecture

The ROI drawing dialog's main purpose is to let users draw geometric shapes over a reference frame and produce a normalized `RegionOfInterest` value object that is completely decoupled from the camera's resolution.

### 2.1 The Coordinate System (Normalization)
- The raw screen coordinates (`e.GetPosition`) are dependent on the window size.
- `RoiCanvas` automatically translates these coordinates into a normalized domain [0, 1] before saving them to the Domain Model (`RegionOfInterest`).
- When resizing the window, `RoiCanvas` re-scales the bindings from the `.Region` property back out to screen pixels by multiplying by `ActualWidth` and `ActualHeight`.

### 2.2 The Custom Control (`RoiCanvas`)
Rather than relying on generic annotation libraries, `RoiCanvas` extends the standard `Canvas`:
1. **Dependency Properties**: Binds two variables:
   - `Region` (`RegionOfInterest`): Two-way bound to the ViewModel.
   - `ActiveShapeType` (`RegionType`): Determines tool behavior (Rectangle vs Polygon).
2. **Mouse Interactions**: 
   - `OnMouseLeftButtonDown`: Starts tracing.
   - `OnMouseMove`: Previews the shape.
   - `OnMouseLeftButtonUp` / Double Click: Commits the shape and updates the `Region` dependency property.
3. **Rendering Shapes**: Uses native `System.Windows.Shapes.Rectangle` and `Polygon` elements internally. It manages only one shape at a time per dialog session.

### 2.3 MVVM Integration
- **`ViewModelBase`**: Provides basic `INotifyPropertyChanged` methods (`SetField`).
- **`RoiDrawingViewModel`**: Holds the `Region`, `ReferenceFrame` (the background image), and user commands (`ClearCommand`, `ConfirmCommand`, `CancelCommand`).
- **`CloseDialogAction`**: An `Action` delegate injected into the ViewModel by the View (`RoiDrawingDialog.xaml.cs`). This allows the ViewModel to request the Window to close without referencing `System.Windows.Window` directly.

---

## 3. Key Quirks & Solutions Implemented

### 3.1 Resolving CS0103 `DependencyProperty` in Converters
- **Issue**: `DependencyProperty.UnsetValue` is in the `System.Windows` namespace. The `EnumToBooleanConverter` threw errors when it wasn't included.
- **Fix**: Added `using System.Windows;` to `EnumToBooleanConverter.cs`.

### 3.2 XAML Designer Errors (XDG0008)
- **Issue**: Sometimes WPF complains that a namespace (`clr-namespace:SeafoodVision.Presentation.*`) doesn't exist during initial development before building.
- **Fix**: Rebuilding the solution (`dotnet build`) usually registers the new WPF Types in the local assembly so the designer can find them.

### 3.3 Canvas Layering over Image
- **Issue**: The drawn shapes need to perfectly align with the `Image` even when the window stretches.
- **Fix**: Used a `Border` containing a `Grid` with `ClipToBounds="True"`. Placing the `Image` and the `RoiCanvas` in the exact same `Grid` cell with `Stretch="Uniform"` on the image guarantees the bounds and layout mechanics align perfectly.

### 3.4 RadioButton Enum Binding
- WPF natively binds `bool` values to `RadioButtons`. We want them bound to an `Enum` (`RegionType.Rectangle` vs `RegionType.Polygon`).
- We built `EnumToBooleanConverter` which checks if the enum property string value equals the `ConverterParameter` defined in XAML.

---

## 4. How to Invoke the Dialog

When integrating this into the main window later (Phase 5), the dialog should be invoked like this:

```csharp
var vm = new RoiDrawingViewModel
{
    ReferenceFrame = /* Get current BitmapSource */,
    ActiveShapeType = RegionType.Rectangle,
    RoiName = "Camera 1 Region"
};

var dialog = new RoiDrawingDialog(vm)
{
    Owner = System.Windows.Application.Current.MainWindow
};

if (dialog.ShowDialog() == true)
{
    // The user clicked Confirm
    RegionOfInterest newRegion = vm.Region;
    // ... add region to recipe ...
}
```

---

## 5. Quick Debug Checklist

**Q: The drawn region appears stretched or misaligned when the window is resized.**
- Check the `Grid` container in `RoiDrawingDialog.xaml`. The `RoiCanvas` and the `Image` must share identical layout boundaries. Ensure `RoiCanvas` has no margins or padding throwing off the 0,0 origin compared to the Image.

**Q: The Confirm button is always disabled/grayed out.**
- `ConfirmCommand` evaluates `CanConfirm()` which requires `Region is not null`. Ensure that finishing a mouse drag (Rectangle) or double-clicking (Polygon) successfully triggers `CommitRectangle()` or `CommitPolygon()` in `RoiCanvas`.

**Q: Switching between Rectangle and Polygon modes leaves "ghost" shapes.**
- Check `OnActiveShapeTypeChanged` in `RoiCanvas.cs`. It should call `.Clear()` on internal collections and drop the existing `Region` state.
