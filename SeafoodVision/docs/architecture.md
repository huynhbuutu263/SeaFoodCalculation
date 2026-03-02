# SeafoodVision – Architecture Documentation

## 1. Full Solution Folder Structure

```
SeafoodVision/
├── SeafoodVision.sln
│
├── src/
│   ├── SeafoodVision.Domain/               # Core layer – no external dependencies
│   │   ├── Entities/
│   │   │   ├── SeafoodItem.cs              # Tracked item aggregate
│   │   │   ├── DetectionFrame.cs           # Frame + detections value object
│   │   │   └── CountingSession.cs          # Persisted session aggregate root
│   │   ├── Interfaces/
│   │   │   ├── IFrameSource.cs             # Camera abstraction
│   │   │   ├── IDetectionService.cs        # AI inference abstraction
│   │   │   ├── ITrackingService.cs         # Multi-object tracker abstraction
│   │   │   ├── ICountingService.cs         # Counting logic abstraction
│   │   │   ├── IPLCService.cs              # PLC communication abstraction
│   │   │   └── IRepository.cs             # Generic + session repository
│   │   └── ValueObjects/
│   │       ├── BoundingBox.cs              # Immutable detection box
│   │       └── TrackingId.cs              # Strongly-typed track identifier
│   │
│   ├── SeafoodVision.Application/          # Use-cases – depends on Domain only
│   │   ├── DTOs/
│   │   │   ├── DetectionResultDto.cs       # Maps Python API JSON → Domain
│   │   │   └── CountingSessionDto.cs       # Read model for UI
│   │   ├── Interfaces/
│   │   │   ├── IInferenceClient.cs         # Application-level AI client contract
│   │   │   └── ICountingOrchestrator.cs    # Top-level pipeline façade
│   │   └── Services/
│   │       ├── CountingOrchestrator.cs     # Pipeline: Acquire → Detect → Track → Count
│   │       ├── TrackingService.cs          # IoU centroid tracker (C#)
│   │       └── CountingService.cs         # Count logic + PLC notification
│   │
│   ├── SeafoodVision.Infrastructure/       # DB + cross-cutting – depends on Domain + Application
│   │   ├── Data/
│   │   │   ├── SeafoodDbContext.cs         # EF Core MSSQL context
│   │   │   └── Repositories/
│   │   │       └── SessionRepository.cs   # ISessionRepository implementation
│   │   └── InfrastructureServiceRegistration.cs
│   │
│   ├── SeafoodVision.Hardware/             # Drivers – depends on Domain only
│   │   ├── Camera/
│   │   │   └── CameraFrameSource.cs       # OpenCV / RTSP camera producer
│   │   ├── PLC/
│   │   │   └── ModbusPLCService.cs        # Modbus TCP via NModbus
│   │   └── HardwareServiceRegistration.cs
│   │
│   ├── SeafoodVision.AI/                   # AI HTTP client – depends on Domain + Application
│   │   ├── Client/
│   │   │   └── InferenceHttpClient.cs     # HttpClient → Python FastAPI
│   │   └── AIServiceRegistration.cs
│   │
│   └── SeafoodVision.Presentation/        # WPF MVVM – depends on Application + Infrastructure + Hardware + AI
│       ├── App.xaml / App.xaml.cs         # Composition root + DI container
│       ├── ViewModels/
│       │   └── MainViewModel.cs           # CommunityToolkit.Mvvm ObservableObject
│       ├── Views/
│       │   ├── MainWindow.xaml            # Live count display + controls
│       │   └── MainWindow.xaml.cs
│       └── appsettings.json
│
├── tests/
│   ├── SeafoodVision.Domain.Tests/         # BoundingBox, SeafoodItem, CountingSession
│   ├── SeafoodVision.Application.Tests/   # TrackingService unit tests
│   └── SeafoodVision.Infrastructure.Tests/# SessionRepository with EF InMemory
│
├── ai/
│   └── inference/                          # Python FastAPI microservice
│       ├── main.py                         # FastAPI app + lifespan
│       ├── Dockerfile
│       ├── requirements.txt
│       ├── models/
│       │   └── detection_result.py        # Pydantic response schema
│       └── services/
│           └── inference_service.py       # ONNX Runtime wrapper
│
└── docs/
    └── architecture.md                    # This file
```

---

## 2. Project References Between Layers

```
Domain           ← (no references)
Application      ← Domain
Infrastructure   ← Domain, Application
Hardware         ← Domain
AI               ← Domain, Application
Presentation     ← Application, Infrastructure, Hardware, AI
```

**Dependency rules (Clean Architecture):**
- Inner layers never reference outer layers.
- `Domain` is dependency-free (pure C# with no NuGet packages beyond the SDK).
- `Application` depends only on `Domain` abstractions (interfaces).
- Outer layers (`Infrastructure`, `Hardware`, `AI`) provide concrete implementations.
- `Presentation` is the composition root and may reference all layers.

---

## 3. Interface Definitions for Core Services

### `IFrameSource` (Domain.Interfaces)
```csharp
public interface IFrameSource : IAsyncDisposable
{
    string CameraId { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    IAsyncEnumerable<(long FrameIndex, DateTime CapturedAt, byte[] Data)>
        ReadFramesAsync(CancellationToken ct = default);
}
```

### `IDetectionService` (Domain.Interfaces)
```csharp
public interface IDetectionService
{
    Task<IReadOnlyList<SeafoodItem>> DetectAsync(byte[] frameData, CancellationToken ct = default);
}
```

### `ITrackingService` (Domain.Interfaces)
```csharp
public interface ITrackingService
{
    Task<IReadOnlyList<SeafoodItem>> UpdateAsync(
        IReadOnlyList<SeafoodItem> rawDetections,
        DateTime frameTimestamp,
        CancellationToken ct = default);
}
```

### `ICountingService` (Domain.Interfaces)
```csharp
public interface ICountingService
{
    int CurrentCount { get; }
    Task<CountingSession> StartSessionAsync(string cameraId, CancellationToken ct = default);
    Task ProcessFrameAsync(DetectionFrame frame, CancellationToken ct = default);
    Task<CountingSession> EndSessionAsync(CancellationToken ct = default);
    event EventHandler<int> CountChanged;
}
```

### `IPLCService` (Domain.Interfaces)
```csharp
public interface IPLCService : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task WriteCountAsync(int count, CancellationToken ct = default);
    Task<bool> ReadLineStatusAsync(CancellationToken ct = default);
    Task SendPulseAsync(ushort coilAddress, CancellationToken ct = default);
}
```

### `ICountingOrchestrator` (Application.Interfaces)
```csharp
public interface ICountingOrchestrator : IAsyncDisposable
{
    CountingSessionDto? CurrentSession { get; }
    Task StartAsync(string cameraId, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    event EventHandler<int> CountUpdated;
}
```

### Python FastAPI Endpoints
```
POST /detect
  Request:  multipart/form-data  { file: <frame bytes> }
  Response: application/json     [ DetectionResult ]

GET  /health
  Response: application/json     { "status": "ok", "model": "<name>" }
```

---

## 4. Threading Model

```
┌──────────────────────────────────────────────────────────────────────┐
│  C# Application Process                                              │
│                                                                      │
│  Thread: LongRunning Task (Camera Capture Loop)                      │
│   └─ Reads frames from VideoCapture at ~30 FPS                       │
│   └─ Writes to BoundedChannel<(long, DateTime, byte[])> (cap=2)      │
│       FullMode = DropOldest  ← maintains real-time latency           │
│                                                                      │
│  Thread: async pipeline (CountingOrchestrator.RunPipelineAsync)      │
│   └─ await foreach on channel reader                                 │
│   └─ await InferenceHttpClient.DetectAsync()  ← HTTP to Python       │
│   └─ await TrackingService.UpdateAsync()      ← CPU-bound, sync     │
│   └─ await CountingService.ProcessFrameAsync()                       │
│       └─ await PLCService.WriteCountAsync()   ← Modbus TCP async     │
│       └─ await SessionRepository.SaveChangesAsync() ← EF async       │
│       └─ raise CountChanged event                                    │
│                                                                      │
│  Thread: UI Dispatcher                                               │
│   └─ MainViewModel.OnCountUpdated()                                  │
│   └─ Dispatcher.InvokeAsync(() => CurrentCount = count)              │
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  Python FastAPI Process (uvicorn, workers=1)                         │
│                                                                      │
│  Uvicorn event loop thread                                           │
│   └─ POST /detect received                                           │
│   └─ loop.run_in_executor(None, _detect_sync, frame_bytes)           │
│       └─ Thread-pool thread: PIL decode + ONNX session.Run()         │
│   └─ Return JSON response                                            │
│                                                                      │
│  Note: onnxruntime.InferenceSession is thread-safe for parallel Run()│
└──────────────────────────────────────────────────────────────────────┘
```

**Key rules enforced:**
- No `Thread.Sleep` — `CancellationToken.WaitHandle.WaitOne(ms)` used in camera loop.
- No synchronous I/O on the async pipeline.
- `SemaphoreSlim(1,1)` guards Modbus TCP writes against concurrent access.
- Channel bounded at capacity 2 with `DropOldest` prevents unbounded memory growth.

---

## 5. Data Flow Diagram

```
┌────────────┐     byte[]      ┌──────────────────┐    HTTP POST    ┌─────────────────────┐
│  Camera /  │ ──────────────► │  C# Inference    │ ──────────────► │  Python FastAPI     │
│  RTSP      │  (BoundedChan) │  HTTP Client     │  /detect        │  + ONNX Runtime     │
└────────────┘                 └──────────────────┘ ◄────────────── └─────────────────────┘
                                        │              JSON []          seafood_detector.onnx
                                        ▼ DetectionResultDto[]
                               ┌──────────────────┐
                               │  TrackingService │  (IoU centroid tracker)
                               │  (C#, in-process)│
                               └──────────────────┘
                                        │ TrackedItems (with TrackingId)
                                        ▼
                               ┌──────────────────┐
                               │ CountingService  │  (crossing-line logic)
                               └──────────────────┘
                                  │            │
                    count change  │            │  count change
                                  ▼            ▼
                         ┌──────────────┐  ┌──────────────────┐
                         │  PLC via     │  │  MSSQL via       │
                         │  Modbus TCP  │  │  EF Core         │
                         │  (NModbus)   │  │  (SessionRepo)   │
                         └──────────────┘  └──────────────────┘
                                  │
                    count event   │
                                  ▼
                         ┌──────────────────────────────┐
                         │  WPF MainWindow (MVVM)       │
                         │  Dispatcher.InvokeAsync      │
                         │  CurrentCount binding → UI   │
                         └──────────────────────────────┘
```

---

## Performance Targets

| Metric                  | Target        |
|-------------------------|---------------|
| Frame rate              | 30 FPS        |
| Inference latency       | ≤ 25 ms/frame |
| End-to-end pipeline lag | ≤ 100 ms      |
| PLC write latency       | ≤ 10 ms       |
| Memory (C# process)     | ≤ 500 MB      |

---

## Configuration (appsettings.json)

| Key                           | Description                        | Default              |
|-------------------------------|------------------------------------|----------------------|
| `ConnectionStrings:Default`   | MSSQL connection string            | localhost / trusted  |
| `Camera:Id`                   | Camera identifier                  | `CAM-01`             |
| `Camera:ConnectionString`     | OpenCV capture arg (index or URL)  | `0` (default webcam) |
| `PLC:Host`                    | PLC IP address                     | `192.168.1.100`      |
| `PLC:Port`                    | Modbus TCP port                    | `502`                |
| `PLC:UnitId`                  | Modbus unit/slave ID               | `1`                  |
| `PLC:CountRegister`           | Holding register address           | `40001`              |
| `InferenceService:BaseUrl`    | Python FastAPI base URL            | `http://localhost:8000` |
| `CONFIDENCE_THRESHOLD` (env)  | Minimum detection confidence       | `0.5`                |
| `MODEL_PATH` (env)            | Path to ONNX model file            | `models/seafood_detector.onnx` |
