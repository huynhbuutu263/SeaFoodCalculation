# Copilot Instructions for SeafoodVision

## Project Overview
SeafoodVision is an industrial real-time seafood counting system.

Tech stack:
- C# (.NET 8)
- WPF (MVVM)
- Python FastAPI for AI inference
- ONNX Runtime
- MSSql
- PLC communication via Modbus TCP

## Architecture Principles
- Clean Architecture
- SOLID principles
- Dependency Injection
- Async/await for all IO operations
- No business logic in UI layer
- Separate hardware layer from vision logic

## Performance Constraints
- Target 30 FPS
- Non-blocking pipeline
- Multithreaded frame processing
- Avoid memory leaks

## Coding Rules
- Use interfaces for all services
- Use ILogger for logging
- Avoid static classes unless utility
- No synchronous IO
- No Thread.Sleep
- Use Task-based async pattern

## Folder Structure

SeafoodVision/
    Presentation/
    Application/
    Domain/
    Infrastructure/
    AI/
    Hardware/
