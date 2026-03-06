# Label Teaching Tool — Solution Design

## Concept

A **vision recipe editor** embedded in SeafoodVision, inspired by Keyence's BarCode Reader tool.  
One **`InspectionRecipe`** (parameter set) contains **N `RoiDefinition`s**. Each ROI has its own independent vision step chain. At runtime the recipe executes all its ROIs either **in parallel** (using `Task.WhenAll`) or **sequentially** (one-by-one) — operator-configurable per recipe.

---

## 1. User Flow

```
MainWindow
  └─▶ [Teach Inspection Recipe] button
        └─▶ RecipeEditorDialog
              │  • Name the recipe, pick Camera, set Execution Mode (Parallel | Sequential)
              │  • Frame source: [📷 Snap from Camera] or [📁 Load from File]
              │  • List of ROI rows   [+ Add ROI]  [× Remove]  [▲][▼]
              │
              └─▶ (for each ROI row) [Edit ROI] → RoiDrawingDialog
              │     • Shows the reference frame with canvas overlay
              │     • Draw Rectangle or Polygon (click-to-add-vertex, dbl-click to close)
              │     • Name the ROI (e.g. "Head Zone", "Body Zone")
              │
              └─▶ (for each ROI row) [Vision Steps] → VisionParamDialog
                    │  • Ordered step chain for THIS ROI's vision flow
                    │  • [+ Add Step] → pick StepType from dropdown
                    │  • Each step: typed parameter controls (from JSON schema)
                    │  • [▶ Run Preview] → intermediate Mat shown on reference frame
                    │
                    └─▶ [Save Recipe] → persists everything to MySQL
```

---

## 2. Domain Model (new entities in `SeafoodVision.Domain`)

```
InspectionRecipe  (aggregate root — "parameter set")
├── Id              : Guid
├── Name            : string          ("Recipe A", "Shrimp Line 1")
├── CameraId        : string
├── IsActive        : bool
├── ExecutionMode   : enum { Sequential, Parallel }
└── RoiDefinitions  : IReadOnlyList<RoiDefinition>   (ordered)

RoiDefinition  (entity owned by InspectionRecipe)
├── Id              : Guid
├── Name            : string          ("Head Zone", "Body Zone")
├── Order           : int
├── Region          : RegionOfInterest  (value object)
│     ├── RegionType  : enum { Rectangle, Polygon }
│     └── Points      : IReadOnlyList<PointF>  (normalised 0-1)
└── Steps           : IReadOnlyList<InspectionStep>

InspectionStep  (entity owned by RoiDefinition)
├── Id              : Guid
├── Order           : int
├── StepType        : enum (see §4)
└── ParametersJson  : string  (JSON object — single column)
```

**Key design choices:**
- `RegionOfInterest` uses normalised [0,1] `PointF` coords → resolution-independent.
- `ParametersJson` is a single JSON string column: `{"ThreshValue":128, "MaxValue":255}`. Simpler schema, no joins, flexible per step type.
- `ExecutionMode` drives whether the runner uses `Task.WhenAll` or a sequential `foreach`.

---

## 3. Database Schema (EF Core / MySQL, new tables)

```
inspection_recipes
  id              CHAR(36)     PK
  name            VARCHAR(100)
  camera_id       VARCHAR(50)
  is_active       TINYINT(1)
  execution_mode  TINYINT      0=Sequential, 1=Parallel

roi_definitions
  id          CHAR(36)  PK
  recipe_id   CHAR(36)  FK → inspection_recipes
  name        VARCHAR(100)
  roi_order   INT
  region_type TINYINT   0=Rectangle, 1=Polygon
  points_json JSON      [{"x":0.1,"y":0.2},...] normalised 0-1

inspection_steps
  id              CHAR(36)  PK
  roi_id          CHAR(36)  FK → roi_definitions
  step_order      INT
  step_type       SMALLINT
  parameters_json JSON      {"ThreshValue":128,...}
```

> **Single JSON column** for parameters — no `step_parameters` rows table needed. Each step type owns its own JSON schema.

---

## 4. Step Types & Parameters

| StepType | Key Parameters | OpenCV op |
|---|---|---|
| `GrayConvert` | _(none)_ | `Cv2.CvtColor → GRAY` |
| `Threshold` | `ThreshValue`, `MaxValue`, `ThreshType` | `Cv2.Threshold` |
| `AdaptiveThreshold` | `BlockSize`, `C`, `Method` | `Cv2.AdaptiveThreshold` |
| `Morphology` | `Operation` (Open/Close/Erode/Dilate), `KernelSize`, `Iterations` | `Cv2.MorphologyEx` |
| `GaussianBlur` | `KernelWidth`, `KernelHeight`, `SigmaX` | `Cv2.GaussianBlur` |
| `Canny` | `Threshold1`, `Threshold2`, `ApertureSize` | `Cv2.Canny` |
| `ContourFilter` | `MinArea`, `MaxArea`, `MinCircularity` | `Cv2.FindContours` + filter |
| `TemplateMatcher` | `TemplatePath`, `Method`, `MatchThreshold` | `Cv2.MatchTemplate` |
| `BlobDetector` | `MinArea`, `MaxArea`, `MinCircularity`, `FilterByColor`, `BlobColor` | `SimpleBlobDetector` |
| `ColorFilter` | `HMin`,`HMax`,`SMin`,`SMax`,`VMin`,`VMax` | HSV mask |
| `DefectDetector` | `Sensitivity`, `MinDefectArea` | Absdiff + contour |

---

## 5. Architecture — New & Modified Projects

```
SeafoodVision.Domain          [MODIFY]
  └─ Entities/
      ├─ InspectionRecipe.cs    [NEW]  aggregate root
      ├─ RoiDefinition.cs       [NEW]  entity
      └─ InspectionStep.cs      [NEW]  entity
  └─ Enums/
      ├─ ExecutionMode.cs       [NEW]  Sequential | Parallel
      ├─ RegionType.cs          [NEW]  Rectangle | Polygon
      └─ StepType.cs            [NEW]  all OpenCV step types
  └─ ValueObjects/
      └─ RegionOfInterest.cs    [NEW]
  └─ Interfaces/
      └─ IRecipeRepository.cs   [NEW]

SeafoodVision.Infrastructure   [MODIFY]
  └─ Data/
      └─ Repositories/
          └─ RecipeRepository.cs [NEW]
      └─ SeafoodDbContext.cs      [MODIFY — add DbSets]
      └─ Configurations/          [NEW — EF IEntityTypeConfiguration per entity]

SeafoodVision.Inspection        [MODIFY]
  └─ Pipeline/
      ├─ StepExecutor.cs        [NEW]  single step → Mat
      ├─ RoiPipelineRunner.cs   [NEW]  runs all steps for one RoiDefinition
      └─ RecipePipelineRunner.cs[NEW]  Sequential or Parallel across all ROIs
  └─ Services/
      └─ InspectionService.cs   [MODIFY — delegate to RecipePipelineRunner]

SeafoodVision.Presentation      [MODIFY]
  └─ Views/
      ├─ RecipeEditorDialog.xaml [NEW]  recipe name + ROI list manager
      ├─ RoiDrawingDialog.xaml   [NEW]  frame canvas + draw tools
      └─ VisionParamDialog.xaml  [NEW]  step chain editor + preview
  └─ ViewModels/
      ├─ RecipeEditorViewModel.cs[NEW]
      ├─ RoiDrawingViewModel.cs  [NEW]
      └─ VisionParamViewModel.cs [NEW]
  └─ Controls/
      ├─ RoiCanvas.xaml          [NEW]  WPF canvas overlay (Rectangle + Polygon)
      └─ StepEditorControl.xaml  [NEW]  DataTemplateSelector per StepType
```

---

## 6. UI Component Detail

### 6.1 `RecipeEditorDialog` (Top-level recipe manager)

```
┌──────────────────────────────────────────────────────────────┐
│  Recipe Name: [Shrimp Line 1 ____________]                   │
│  Camera: [CAM-01 ▾]   Execution: ● Parallel  ○ Sequential   │
│                                                              │
│  Reference Frame:  [📷 Snap from Camera]  [📁 Load from File]│
│  ┌────────────────────────────────────────────────────────┐  │
│  │  (thumbnail of reference frame)                       │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  ROI Definitions:                         [+ Add ROI]        │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ 1 │ Head Zone  │ Rectangle │ [✏ Draw ROI] [⚙ Steps] [×]│  │
│  │ 2 │ Body Zone  │ Polygon   │ [✏ Draw ROI] [⚙ Steps] [×]│  │
│  │ 3 │ Tail Zone  │ Rectangle │ [✏ Draw ROI] [⚙ Steps] [×]│  │
│  └────────────────────────────────────────────────────────┘  │
│                              [Cancel]       [💾 Save Recipe] │
└──────────────────────────────────────────────────────────────┘
```

### 6.2 `RoiDrawingDialog` (per-ROI canvas)

```
┌─────────────────────────────────────────────────────────────┐
│  ROI Name: [Head Zone _________]                            │
│  Shape: ● Rectangle  ○ Polygon      [🗑 Clear]              │
│  ┌───────────────────────────────────────────────────────┐  │
│  │                                                       │  │
│  │    ┌ · · · · · · · · ┐  ← draggable handles          │  │
│  │    ·                 ·                                │  │
│  │    └ · · · · · · · · ┘                                │  │
│  └───────────────────────────────────────────────────────┘  │
│                                    [Cancel]    [✔ Confirm]  │
└─────────────────────────────────────────────────────────────┘
```

- `RoiCanvas` = WPF `Canvas` overlay on `Image`. Rectangle: drag. Polygon: click vertices, dbl-click to close.
- Normalised [0,1] coords stored; rendered back at display resolution on re-open.

### 6.3 `VisionParamDialog` (per-ROI step chain editor)

```
┌──────────────────────────────────────────────────────────────┐
│ ROI: "Head Zone"  (Rectangle)                                │
│ Steps:                                          [+ Add Step] │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ 1 │ GrayConvert   │ (no params)              [▲][▼][×]  │ │
│ │ 2 │ GaussianBlur  │ KW:5  KH:5  σ:1.0        [▲][▼][×]  │ │
│ │ 3 │ Threshold     │ Val:128  Max:255  OTSU    [▲][▼][×]  │ │
│ │ 4 │ Morphology    │ Open  K:3  Iter:2         [▲][▼][×]  │ │
│ │ 5 │ ContourFilter │ Area:100-5000  Circ:0.4   [▲][▼][×]  │ │
│ └──────────────────────────────────────────────────────────┘ │
│  Params for selected step ──────────────────────────────── │ │
│  │ Threshold Value [128 ─────] Max Value [255 ───]         │ │
│  │ Type: ● OTSU  ○ Binary  ○ Binary_Inv                    │ │
│  └─────────────────────────────────────────────────────── │ │
│                                        [▶ Run Preview]       │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  intermediate Mat result (BitmapSource)              │   │
│  └──────────────────────────────────────────────────────┘   │
│                                    [Cancel]   [✔ Confirm]   │
└──────────────────────────────────────────────────────────────┘
```

- `DataTemplateSelector` picks the right parameter editor panel per `StepType`.
- **[▶ Run Preview]** feeds the reference frame through `RoiPipelineRunner` up to the selected step and renders the `Mat` as `BitmapSource`.
- Selecting a step row highlights its params in the lower panel (master-detail).

---

## 7. Key Services: Pipeline Runners

```csharp
// RoiPipelineRunner — runs the step chain for ONE RoiDefinition
public class RoiPipelineRunner
{
    // Optionally run only up to stepLimit (for preview)
    public Mat Run(Mat fullFrame, RoiDefinition roi, int stepLimit = int.MaxValue)
    {
        var rect = roi.Region.ToCvRect(fullFrame.Width, fullFrame.Height);
        Mat current = new Mat(fullFrame, rect).Clone();
        foreach (var step in roi.Steps.OrderBy(s => s.Order).Take(stepLimit))
            current = StepExecutor.Execute(current, step);
        return current;
    }
}
```

```csharp
// RecipePipelineRunner — dispatches across ALL ROIs (Sequential or Parallel)
public class RecipePipelineRunner
{
    private readonly RoiPipelineRunner _roiRunner;

    public async Task<IReadOnlyList<RoiResult>> RunAsync(
        Mat fullFrame, InspectionRecipe recipe, CancellationToken ct)
    {
        if (recipe.ExecutionMode == ExecutionMode.Parallel)
        {
            var tasks = recipe.RoiDefinitions
                .Select(roi => Task.Run(() =>
                    new RoiResult(roi, _roiRunner.Run(fullFrame, roi)), ct));
            return await Task.WhenAll(tasks);
        }
        else
        {
            var results = new List<RoiResult>();
            foreach (var roi in recipe.RoiDefinitions.OrderBy(r => r.Order))
                results.Add(new RoiResult(roi, _roiRunner.Run(fullFrame, roi)));
            return results;
        }
    }
}
```

```csharp
// StepExecutor — maps StepType → OpenCV operation
public static class StepExecutor
{
    public static Mat Execute(Mat src, InspectionStep step) => step.StepType switch
    {
        StepType.GrayConvert     => ToGray(src),
        StepType.Threshold       => ApplyThreshold(src, step),
        StepType.AdaptiveThreshold => ApplyAdaptiveThreshold(src, step),
        StepType.GaussianBlur    => ApplyBlur(src, step),
        StepType.Morphology      => ApplyMorphology(src, step),
        StepType.Canny           => ApplyCanny(src, step),
        StepType.ContourFilter   => ApplyContourFilter(src, step),
        StepType.BlobDetector    => ApplyBlobDetector(src, step),
        StepType.TemplateMatcher => ApplyTemplateMatcher(src, step),
        StepType.ColorFilter     => ApplyColorFilter(src, step),
        StepType.DefectDetector  => ApplyDefectDetector(src, step),
        _ => throw new NotSupportedException($"Unknown step: {step.StepType}")
    };
    // Each method deserialises step.ParametersJson via System.Text.Json
}
```

---

## 8. How [InspectionService](file:///c:/Wisely/C%23/Project/SeafoodVision/src/SeafoodVision.Inspection/Services/InspectionService.cs#13-17) Uses Teaching Recipes

```csharp
// Modified InspectionService.InspectAsync
public async Task<IReadOnlyList<SeafoodItem>> InspectAsync(
    byte[] frameData, IReadOnlyList<SeafoodItem> detections, CancellationToken ct)
{
    // Active recipe is loaded once at startup and cached (invalidated on save)
    var recipe = await _recipeCache.GetActiveAsync(_cameraId, ct);
    if (recipe is null) return detections;  // no recipe → pass-through

    using var bgr = Cv2.ImDecode(frameData, ImreadModes.Color);

    // Run ALL ROI pipelines (sequential or parallel per recipe config)
    var roiResults = await _recipePipelineRunner.RunAsync(bgr, recipe, ct);

    // Filter detections whose centroid falls in a FAILED roi result
    var failedRois = roiResults.Where(r => !r.IsPassed).ToHashSet();
    return detections
        .Where(item => !failedRois.Any(r =>
            r.Roi.Region.Contains(item.BoundingBox.CenterX, item.BoundingBox.CenterY)))
        .ToList()
        .AsReadOnly();
}
```

---

## 9. Phased Roadmap ⭐ Start with Phase 1

| Phase | Scope | Deliverable |
|---|---|---|
| **1 ← START** | Domain + DB | `InspectionRecipe`, `RoiDefinition`, `InspectionStep` entities; EF config; `IRecipeRepository` + MySQL migration |
| **2** | Inspection pipeline | `StepExecutor` (all step types) + `RoiPipelineRunner` + `RecipePipelineRunner` (Parallel/Sequential) |
| **3** | ROI Drawing Dialog | `RoiDrawingDialog` + `RoiCanvas` control (Rectangle first, Polygon next) |
| **4** | Vision Param Dialog | `VisionParamDialog` + `StepEditorControl` (DataTemplateSelector) + live preview |
| **5** | Recipe Editor Dialog | `RecipeEditorDialog` — ties ROI list + Vision Params + frame snap/load together |
| **6** | Integration | Wire [InspectionService](file:///c:/Wisely/C%23/Project/SeafoodVision/src/SeafoodVision.Inspection/Services/InspectionService.cs#13-17) → `RecipePipelineRunner`; `IMemoryCache` recipe cache with invalidation on save |
| **7** | Polish | Drag-reorder steps, copy recipe between cameras, export/import JSON |

**Why Phase 1 first:** `InspectionRecipe` is the contract that every other piece (ViewModels, pipeline runner, EF) depends on. Locking down the entity shape early avoids cascading refactors later.

---

## 10. Key Technical Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Parameter storage | Single `parameters_json` JSON column per step | No extra table/joins; flexible per step type; easy to add new step types without migration |
| ROI coordinates | Normalised [0,1] `PointF` | Resolution-independent; survives camera resolution change |
| Parallel execution | `Task.WhenAll` of `Task.Run` per ROI | Each ROI pipeline is CPU-bound (OpenCV); offload to thread pool |
| Step preview frame | Camera snap via `IFrameSource` **or** load from file | `RoiDrawingViewModel` offers both; stores the Mat as the recipe's reference frame |
| Recipe caching | `IMemoryCache` keyed by `cameraId` | Avoids DB hit on every frame; `RecipeRepository.Save` triggers cache invalidation |
| Polygon hit-test | `Cv2.PointPolygonTest` | Reuse existing OpenCV dependency; handles convex and concave polygons |
| Template images | File path stored in `parameters_json` | Simple to start; operator browses to file during teaching |
