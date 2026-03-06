---
name: inspection-recipe-pipeline-phase2
description: >
  Reference guide for Phase 2 of the Label Teaching Tool implementation.
  Covers the Inspection Pipeline layer (StepExecutor, RoiPipelineRunner,
  RecipePipelineRunner) and Parameter Models.
  Use this to debug OpenCV operations, JSON parameter deserialization, 
  and multithreaded ROI execution.
---

# Phase 2 — Inspection Pipeline

## 1. What Was Added

### New files at a glance

```
src/SeafoodVision.Inspection/
  Pipeline/
    StepExecutor.cs            ← Maps StepType to actual OpenCV operations
    RoiPipelineRunner.cs       ← Executes the sequential step chain for a SINGLE ROI
    RecipePipelineRunner.cs    ← Orchestrates ALL ROIs (Sequential or Parallel)
    Models/
      StepParameters.cs        ← Strongly-typed parameter classes for JSON deserialization
      RoiResult.cs             ← Record holding execution pass/fail & final Mat
  InspectionServiceRegistration.cs ← MODIFIED: registered pipeline runners
```

---

## 2. Pipeline Architecture

The execution flows from top to bottom:

1. **`RecipePipelineRunner.RunAsync`**:
   - Takes a `Mat fullFrame` and an `InspectionRecipe`.
   - If `ExecutionMode.Parallel`, uses `Task.WhenAll` to spawn ThreadPool threads for each ROI.
   - If `ExecutionMode.Sequential`, loops through ROIs on the calling thread.
   - Merges results into an `IReadOnlyList<RoiResult>`.

2. **`RoiPipelineRunner.Run`**:
   - Takes the `fullFrame` and one `RoiDefinition`.
   - Resolves the normalized `RegionOfInterest` [0,1] back to pixel bounds and crops.
   - Loops through the `InspectionStep`s ordered by `Order`.
   - **Critical:** Disposes of intermediate `Mat` objects immediately after each step to prevent memory leaks.

3. **`StepExecutor.Execute`**:
   - `switch` on `StepType`.
   - Deserializes `ParametersJson` dynamically into the corresponding class (e.g., `ThresholdParams`).
   - Calls the underlying `OpenCvSharp.Cv2` method.
   - Returns the new transformed `Mat`.

---

## 3. Parameter Models (`StepParameters.cs`)

Each `StepType` has a corresponding parameter class. 
**Note:** These are standard `public class` types with default property initializers to satisfy the `new()` constraint in `JsonSerializer`. Do NOT change them to C# 9 `records` with primary constructors, as that breaks the parameterless constructor required for deserialization.

| StepType Enum | Parameter Class | Key Defaults |
|---|---|---|
| `GrayConvert` | `GrayConvertParams` | *(none)* |
| `ColorFilter` | `ColorFilterParams` | HMin:0, HMax:179, SMin:0... |
| `GaussianBlur` | `GaussianBlurParams` | Kernel Width/Height: 5, SigmaX: 0 |
| `MedianBlur` | `MedianBlurParams` | KernelSize: 5 |
| `Threshold` | `ThresholdParams` | ThreshValue: 127, MaxValue: 255 |
| `AdaptiveThreshold` | `AdaptiveThresholdParams` | BlockSize: 11, C: 2, GaussianC |
| `Morphology` | `MorphologyParams` | Operation: Open, KernelSize: 3, Iterations: 1 |
| `Canny` | `CannyParams` | Threshold1: 100, Threshold2: 200, ApertSize: 3 |
| `ContourFilter` | `ContourFilterParams` | MinArea: 100, MinCircularity: 0.0 |
| `BlobDetector` | `BlobDetectorParams` | MinArea: 100, BlobColor: 255 |
| `TemplateMatcher`| `TemplateMatcherParams` | MatchThreshold: 0.8, CCoeffNormed |
| `DefectDetector` | `DefectDetectorParams` | Sensitivity: 30, MinDefectArea: 50 |

---

## 4. Key OpenCV Rules & Quirks Applied

During implementation, several OpenCVSharp quirks had to be handled:

### 4.1 Memory Leaks (`Mat` disposal)
Every OpenCV `Mat` allocates unmanaged memory. In `RoiPipelineRunner`, the loop is designed to `.Dispose()` the previous intermediate `Mat` frame *immediately* after the next step is executed. 
➡ **Debug Tip:** If memory usage skyrockets slowly over time, verify that `RoiResult.ResultMat` is being disposed by the caller, and no intermediate MATs are left hanging in `StepExecutor`.

### 4.2 Handling single-channel vs multi-channel
Operations like `Threshold` or `Canny` require a 1-channel grayscale image, but the source can be a 3-channel BGR from the previous step.
- We aggressively fallback to `ToGray(src)` inside operations that strictly demand it.
- `ToGray` checks `src.Channels() == 1` to prevent double-conversion overhead.

### 4.3 `Mat.Zeros` / `MatExpr` Casting (CS1503)
Some operations like `Cv2.DrawContours` or `Cv2.Circle` expect an actual `Mat`, but `Mat.Zeros()` returns a lightweight `MatExpr` proxy.
- **Fixed:** We initialize blank masks explicitly utilizing `new Mat(size, type, Scalar.Black)` instead of `Mat.Zeros`.

### 4.4 `Cv2.Circle` Filled Thickness (CS1503)
Passing `Cv2.FILLED` (an enum) directly to the thickness int parameter fails in some versions.
- **Fixed:** We explicitly pass `thickness: -1` to guarantee a solid sphere.

### 4.5 Kernel Sizes
`GaussianBlur` and `MedianBlur` operations crash if the kernel size is even.
- **Fixed:** `StepExecutor` automatically handles even values forcing `size % 2 == 0 ? size + 1 : size`.

---

## 5. DI Registration

Runners were registered in `InfrastructureServiceRegistration.AddInspectionServices()`:
```csharp
services.AddTransient<RoiPipelineRunner>();
services.AddTransient<RecipePipelineRunner>();
services.AddSingleton<IInspectionService, InspectionService>();
```
➡ **Note:** Runners are `Transient` because they store no state and execution spans are short-lived.

---

## 6. Quick Debug Checklist

**Q: Compilation error CS0310 "must be a non-abstract type with a public parameterless constructor"?**
- You tried to make a new parameter class a `record (int myProp)` rather than `class { public int myProp {get;set;} }`. Deserialization requires `public class()` with zero arguments.

**Q: Compilation error CS1503 "cannot convert from MatExpr to InputOutputArray"?**
- You assigned a `MatExpr` (like `src * 2` or `Mat.Zeros`) to a normal `Mat` variable, and passed it to a function. Just use `new Mat(..., Scalar.Black)`.

**Q: Inspection steps are silently skipping / doing nothing?**
- Ensure `ParametersJson` stored in the database has exact matching property names (e.g. `{"ThreshValue": 150}`).
- By design, `StepExecutor.DeserializeParams<T>` swallows exceptions and returns default configs if the JSON is garbled. Check your DB values!

**Q: "OpenCV Exception: sizes of input arguments do not match"?**
- The `DefectDetector` or `TemplateMatcher` loaded an image that doesn't match the dimensions of the cropped ROI `Mat`. Resize or align the template image correctly. 

**Q: Memory usage creeping up on the server?**
- Make sure whoever calls `RecipePipelineRunner.RunAsync()` iterates over the returned `IReadOnlyList<RoiResult>` and explicitly calls `.Dispose()` on every result when done.
