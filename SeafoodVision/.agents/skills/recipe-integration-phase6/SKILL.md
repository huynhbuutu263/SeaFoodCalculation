---
name: inspection-recipe-integration-phase6
description: >
  Reference guide for Phase 6 of the Label Teaching Tool implementation.
  Covers wiring InspectionService to RecipePipelineRunner, the IMemoryCache
  recipe cache with invalidation on save, and the DI registration changes.
---

# Phase 6 — Integration: InspectionService + Recipe Cache

## 1. What Was Added

### New files at a glance

```
src/SeafoodVision.Inspection/
  Cache/
    IRecipeCache.cs            ← Interface: GetActiveAsync + Invalidate
    RecipeCacheService.cs      ← IMemoryCache-backed implementation
  Services/
    InspectionService.cs       ← MODIFIED: wired to IRecipeCache + RecipePipelineRunner
  InspectionServiceRegistration.cs ← MODIFIED: registers AddMemoryCache + IRecipeCache
```

### Modified files at a glance

```
src/SeafoodVision.Presentation/
  App.xaml.cs                  ← Configure<InspectionOptions> from Camera:Id
  ViewModels/
    MainViewModel.cs           ← Injects IRecipeCache when opening RecipeEditorDialog
    RecipeEditorViewModel.cs   ← Accepts IRecipeCache; invalidates cache on save
```

---

## 2. Architecture

```
CountingOrchestrator
  └─► InspectionService.InspectAsync(frameData, detections)
        │
        ├─ IRecipeCache.GetActiveAsync(cameraId)
        │     │  cache hit → return from IMemoryCache (no DB round-trip per frame)
        │     │  cache miss → IRecipeRepository.GetActiveAsync(cameraId) → store + return
        │
        ├─ if (recipe is null) → pass-through (return all detections unchanged)
        │
        ├─ RecipePipelineRunner.RunAsync(bgr, recipe, ct)
        │     │  runs all ROI pipelines (Sequential or Parallel per recipe.ExecutionMode)
        │     └─► RoiResult[] — each result carries IsPassed flag
        │
        └─ Filter detections:
              Remove items whose BoundingBox centroid falls inside a FAILED ROI
              Return filtered list to CountingOrchestrator
```

---

## 3. IRecipeCache Contract

```csharp
public interface IRecipeCache
{
    // Returns most-recently-updated active recipe; null = pass-through mode
    Task<InspectionRecipe?> GetActiveAsync(string cameraId, CancellationToken ct = default);

    // Invalidate after saving a recipe in the editor so the next frame re-reads from DB
    void Invalidate(string cameraId);
}
```

---

## 4. RecipeCacheService Behaviour

| Scenario | Behaviour |
|---|---|
| Cache hit | Returns cached `InspectionRecipe?` immediately (no DB query) |
| Cache miss | Calls `IRecipeRepository.GetActiveAsync()` → picks the most-recently-updated active recipe → stores under key `recipe:active:{cameraId}` with **5-minute sliding expiry** |
| No active recipe | Caches `null` for 5 min to prevent repeated DB hits in pass-through mode |
| Save from editor | `IRecipeCache.Invalidate(cameraId)` called → removes cache entry → next frame re-reads from DB |

---

## 5. InspectionService Logic

```csharp
var recipe = await _recipeCache.GetActiveAsync(_cameraId, ct);
if (recipe is null) return detections;   // pass-through

using var bgr = Cv2.ImDecode(frameData, ImreadModes.Color);
var roiResults = await _pipelineRunner.RunAsync(bgr, recipe, ct);

try
{
    var failedRois = roiResults.Where(r => !r.IsPassed).ToList();
    if (failedRois.Count == 0) return detections;

    return detections
        .Where(item => !failedRois.Any(r =>
            r.Roi.Region.Contains(item.BoundingBox.CenterX, item.BoundingBox.CenterY)))
        .ToList().AsReadOnly();
}
finally
{
    foreach (var r in roiResults) r.Dispose();   // release unmanaged Mats
}
```

---

## 6. Configuration

`InspectionOptions.CameraId` is bound from `Camera:Id` in `appsettings.json`:

```csharp
// App.xaml.cs
services.Configure<InspectionOptions>(opts =>
    opts.CameraId = configuration["Camera:Id"] ?? "CAM-01");
```

This keeps `InspectionService` decoupled from the Hardware layer while still reading the same camera ID.

---

## 7. DI Registration Summary

```csharp
// InspectionServiceRegistration.AddInspectionServices()
services.AddTransient<RoiPipelineRunner>();
services.AddTransient<RecipePipelineRunner>();
services.AddMemoryCache();                         // IMemoryCache (shared singleton)
services.AddSingleton<IRecipeCache, RecipeCacheService>();
services.AddSingleton<IInspectionService, InspectionService>();
```

- `IMemoryCache` is registered as a **singleton** by `AddMemoryCache()` — the same in-memory store is shared across the application lifetime.
- `IRecipeCache` is **singleton** — it holds a reference to the singleton `IMemoryCache`.
- `IInspectionService` is **singleton** — stateless except for the injected `_cameraId` string.
- `RecipePipelineRunner` / `RoiPipelineRunner` remain **transient** — they carry no mutable state.

---

## 8. Cache Invalidation from the Recipe Editor

`RecipeEditorViewModel` accepts an optional `IRecipeCache` in its constructor:

```csharp
public RecipeEditorViewModel(
    IRecipeRepository repository,
    RoiPipelineRunner pipelineRunner,
    IRecipeCache? recipeCache = null)  // optional for backward compatibility
```

After `SaveChangesAsync()` succeeds:

```csharp
var cameraIds = Recipes.Select(r => r.CameraId).Distinct();
foreach (var cam in cameraIds)
    _recipeCache?.Invalidate(cam);
```

`MainViewModel.OnOpenRecipeEditor()` resolves the cache from DI and passes it to the editor:

```csharp
var cache = _serviceProvider.GetService<IRecipeCache>();
var editorVm = new RecipeEditorViewModel(recipeRepo, runner, cache);
```

---

## 9. Quick Debug Checklist

**Q: Inspection is not filtering any detections even after saving a recipe.**
1. Verify the recipe is marked `IsActive = true` in the DB or editor.
2. Check that `Camera:Id` in `appsettings.json` matches the `CameraId` stored on the recipe.
3. Call `_recipeCache.Invalidate(cameraId)` if you edited the DB manually — the UI save does this automatically.

**Q: Cache is never invalidated after clicking "Save All To Database".**
- Ensure `IRecipeCache` was resolved from DI and passed to `RecipeEditorViewModel` (check `MainViewModel.OnOpenRecipeEditor`).
- The `IRecipeCache` constructor parameter is optional — if `null` is passed, invalidation is silently skipped.

**Q: Memory keeps growing after many frame inspections.**
- Check that the `finally` block in `InspectionService.InspectAsync` is always reached (it disposes the `RoiResult` Mats).
- The `RecipePipelineRunner` returns new Mats per call; `InspectionService` owns their disposal.

**Q: Parallel ROI execution causes race conditions on the Mat.**
- `RecipePipelineRunner` passes a read-only `fullFrame` to each `Task.Run`. `RoiPipelineRunner.Run()` immediately clones the cropped sub-region (`new Mat(fullFrame, safeRect).Clone()`), so concurrent reads on `fullFrame` are safe.
