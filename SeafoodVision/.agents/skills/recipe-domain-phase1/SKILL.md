---
name: inspection-recipe-domain-phase1
description: >
  Reference guide for Phase 1 of the Label Teaching Tool implementation.
  Covers the InspectionRecipe domain layer (entities, enums, value objects,
  repository) and the corresponding EF Core / MySQL infrastructure.
  Use this to debug entity relationships, EF mappings, DI registration, and
  JSON parameter serialisation.
---

# Phase 1 — Inspection Recipe Domain & Infrastructure

## 1. What Was Added

### New files at a glance

```
src/SeafoodVision.Domain/
  Enums/
    ExecutionMode.cs          ← Sequential | Parallel (byte, persisted)
    RegionType.cs             ← Rectangle | Polygon   (byte, persisted)
    StepType.cs               ← 12 OpenCV step types  (short, persisted)
  ValueObjects/
    RegionOfInterest.cs       ← normalised [0,1] polygon/rect + Contains()
  Entities/
    InspectionRecipe.cs       ← aggregate root   (owns N RoiDefinitions)
    RoiDefinition.cs          ← child entity     (owns N InspectionSteps)
    InspectionStep.cs         ← leaf entity      (parameters_json column)
  Interfaces/
    IRecipeRepository.cs      ← extends IRepository<InspectionRecipe>

src/SeafoodVision.Infrastructure/
  Data/
    SeafoodDbContext.cs       ← MODIFIED: added 3 new DbSets + ApplyConfiguration
    Configurations/
      InspectionRecipeConfiguration.cs
      RoiDefinitionConfiguration.cs
      InspectionStepConfiguration.cs
    Repositories/
      RecipeRepository.cs     ← IRecipeRepository implementation
  InfrastructureServiceRegistration.cs  ← MODIFIED: +AddScoped<IRecipeRepository>
```

---

## 2. Entity Hierarchy

```
InspectionRecipe  (aggregate root)
│  Id            Guid
│  Name          string
│  CameraId      string
│  IsActive      bool
│  ExecutionMode enum  { Sequential=0, Parallel=1 }
│  CreatedAt     DateTime (UTC)
│  UpdatedAt     DateTime (UTC)
│
└─► RoiDefinition  (1..N, ordered by Order)
    │  Id            Guid
    │  Name          string
    │  Order         int   (1-based)
    │  RecipeId      Guid  (FK)
    │  Region        RegionOfInterest  [value object, not a DB column directly]
    │
    └─► InspectionStep  (1..N, ordered by Order)
           Id              Guid
           Order           int   (1-based)
           StepType        enum  (short in DB)
           ParametersJson  string  (JSON object)
           RoiDefinitionId Guid  (FK)
```

---

## 3. Database Tables

### `inspection_recipes`
| Column           | Type         | Notes                          |
|------------------|--------------|--------------------------------|
| `id`             | CHAR(36)     | PK, no auto-generate           |
| `name`           | VARCHAR(200) | required                       |
| `camera_id`      | VARCHAR(100) | required, indexed with IsActive|
| `is_active`      | TINYINT(1)   |                                |
| `execution_mode` | TINYINT      | 0=Sequential, 1=Parallel       |
| `created_at`     | DATETIME     | UTC                            |
| `updated_at`     | DATETIME     | UTC                            |

### `roi_definitions`
| Column        | Type         | Notes                              |
|---------------|--------------|------------------------------------|
| `id`          | CHAR(36)     | PK                                 |
| `recipe_id`   | CHAR(36)     | FK → inspection_recipes (CASCADE)  |
| `name`        | VARCHAR(200) |                                    |
| `roi_order`   | INT          |                                    |
| `region_type` | TINYINT      | 0=Rectangle, 1=Polygon             |
| `points_json` | JSON         | `[{"x":0.1,"y":0.2},...]`          |

### `inspection_steps`
| Column            | Type     | Notes                            |
|-------------------|----------|----------------------------------|
| `id`              | CHAR(36) | PK                               |
| `roi_id`          | CHAR(36) | FK → roi_definitions (CASCADE)   |
| `step_order`      | INT      |                                  |
| `step_type`       | SMALLINT | maps to `StepType` enum          |
| `parameters_json` | JSON     | `{}` default; schema per StepType|

> **Cascade deletes:** Recipe delete → cascades to ROIs → cascades to Steps.

---

## 4. StepType Enum Values (stable — never renumber)

| Value | Name               | Key Parameters                                              |
|-------|--------------------|-------------------------------------------------------------|
| 0     | GrayConvert        | _(none)_                                                    |
| 1     | ColorFilter        | HMin, HMax, SMin, SMax, VMin, VMax                          |
| 10    | GaussianBlur       | KernelWidth (odd), KernelHeight (odd), SigmaX               |
| 11    | MedianBlur         | KernelSize (odd)                                            |
| 20    | Threshold          | ThreshValue, MaxValue, ThreshType (Binary/BinaryInv/Otsu)   |
| 21    | AdaptiveThreshold  | MaxValue, Method, ThreshType, BlockSize (odd), C            |
| 30    | Morphology         | Operation, KernelSize, Iterations                           |
| 40    | Canny              | Threshold1, Threshold2, ApertureSize (3/5/7)                |
| 50    | ContourFilter      | MinArea, MaxArea, MinCircularity, MaxCircularity             |
| 51    | BlobDetector       | MinArea, MaxArea, MinCircularity, FilterByColor, BlobColor  |
| 60    | TemplateMatcher    | TemplatePath, Method, MatchThreshold                        |
| 70    | DefectDetector     | ReferencePath, Sensitivity, MinDefectArea                   |

---

## 5. RegionOfInterest — Key Points

- All coordinates are **normalised [0, 1]** relative to frame dimensions.
- **Rectangle**: exactly **2 points** — `[TopLeft, BottomRight]`.
- **Polygon**: **≥ 3 points** in drawing order.
- `Contains(nx, ny)` — Rectangle uses bounds check; Polygon uses ray-casting.
- `ToPixelRect(w, h)` — returns bounding `System.Drawing.Rectangle` in pixels.
- **Not an EF-owned type** — stored via two EF shadow properties on `RoiDefinition`:
  - `"RegionTypeRaw"` (byte)
  - `"PointsJson"` (JSON string)
- Rehydration after DB load **must** happen in `RecipeRepository` — see §8.

---

## 6. EF Core Mapping — Edge Cases

### 6.1 Private backing fields
`InspectionRecipe._rois` and `RoiDefinition._steps` are private `List<T>` fields.  
EF accesses them via shadow property names in `.HasMany<T>("_rois")`.  
➡ If you see `Navigation property '_rois' could not be found`, check that the field name matches exactly.

### 6.2 RegionOfInterest shadow columns
`RoiDefinitionConfiguration` ignores `Region` (`builder.Ignore(r => r.Region)`)  
and instead maps two shadow properties: `"RegionTypeRaw"` and `"PointsJson"`.  
The `RoiDefinition.Region` object is **not automatically populated on read** — it must be rebuilt from the shadow columns in the repository.  
➡ **TODO Phase 6**: add rehydration logic in `RecipeRepository.GetByIdAsync` / `GetAllAsync`.

### 6.3 ValueGeneratedNever
All three entities use `ValueGeneratedNever()` on Id.  
EF will **not** try to read a DB-generated key after insert.  
➡ Always set `Id = Guid.NewGuid()` in the `Create()` factory — never leave it as `Guid.Empty`.

### 6.4 JSON columns require MySQL 8+
`parameters_json` and `points_json` use `HasColumnType("json")`.  
The project targets `MySqlServerVersion(8, 0, 0)` — this is fine.  
➡ Do not downgrade the server version constant or JSON columns will not be created correctly.

---

## 7. DI Registration

Registered in `InfrastructureServiceRegistration.AddInfrastructure()`:

```csharp
services.AddScoped<ISessionRepository, SessionRepository>();   // existing
services.AddScoped<IRecipeRepository, RecipeRepository>();     // NEW
```

Tables are auto-created by `EnsureDbCreatedAsync()` at startup (called in `App.xaml.cs`).  
This is **not migrations** — schema changes require dropping and recreating the DB (dev only).

---

## 8. Known Gaps / TODOs for Later Phases

| # | Gap | When to fix |
|---|-----|-------------|
| 1 | `RoiDefinition.Region` is not rehydrated after a DB read | Phase 6 integration |
| 2 | No unit tests for new entities | Add to `SeafoodVision.Domain.Tests` |
| 3 | `RecipeRepository.UpdateAsync` calls `PopulateShadowPropertiesAsync` synchronously via `.GetAwaiter().GetResult()` — safe because the method is in fact synchronous, but should be refactored | Phase 6 |
| 4 | `IRecipeRepository.DeleteAsync` does not call `SaveChangesAsync` — callers must call it | Document on interface |

---

## 9. Quick Debug Checklist

**Q: Tables not created on startup?**
- Check MySQL user has `CREATE TABLE` privilege.
- Verify `appsettings.json` → `ConnectionStrings:DefaultConnection` points to correct DB.
- Set `LogLevel` to `Debug` in `appsettings.json` to see EF SQL output.

**Q: `'_rois' navigation not found` EF error?**
- Field name in `InspectionRecipe` and the string in `HasMany<RoiDefinition>("_rois")` must match exactly (case-sensitive).

**Q: `Region` is always null after loading from DB?**
- Expected — rehydration not yet implemented (see §8 gap #1).
- Workaround: read shadow properties via `entry.Property("RegionTypeRaw")` and `entry.Property("PointsJson")` manually after a query.

**Q: `ParametersJson` is invalid JSON at runtime?**
- `InspectionStep.Create()` defaults to `"{}"` if no JSON is passed.
- Before calling `StepExecutor.Execute()`, wrap `JsonSerializer.Deserialize` in a try/catch and log the raw `ParametersJson` string on failure.

**Q: Wrong StepType persisted to DB?**
- `StepType` is stored as `short`. Verify the numeric value via:
  ```sql
  SELECT id, step_type, parameters_json FROM inspection_steps;
  ```
- Cross-reference with the enum table in §4.

**Q: ExecutionMode always 0 (Sequential) regardless of setting?**
- Stored as `byte`. Verify:
  ```sql
  SELECT id, name, execution_mode FROM inspection_recipes;
  ```
- Check that you called `recipe.SetExecutionMode(ExecutionMode.Parallel)` before saving.
