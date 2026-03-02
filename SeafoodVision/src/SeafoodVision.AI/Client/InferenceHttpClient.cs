using Microsoft.Extensions.Logging;
using SeafoodVision.Application.DTOs;
using SeafoodVision.Application.Interfaces;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;
using SeafoodVision.Domain.ValueObjects;
using System.Net.Http.Json;

namespace SeafoodVision.AI.Client;

/// <summary>
/// HTTP client that sends frames to the Python FastAPI inference service
/// and maps the JSON response to domain DTOs.
///
/// Registered as a typed HttpClient via IHttpClientFactory for resilience
/// (retry + circuit-breaker policies should be added via Polly).
/// </summary>
public sealed class InferenceHttpClient : IInferenceClient, IDetectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InferenceHttpClient> _logger;

    public InferenceHttpClient(HttpClient httpClient, ILogger<InferenceHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DetectionResultDto>> InferAsync(
        byte[] frameData,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(frameData), "file", "frame.jpg");

        var response = await _httpClient.PostAsync("/detect", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<DetectionResultDto>>(cancellationToken)
                      ?? [];

        _logger.LogDebug("Inference returned {Count} detections", results.Count);
        return results.AsReadOnly();
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inference service health check failed");
            return false;
        }
    }

    /// <summary>
    /// Implements <see cref="IDetectionService"/> so that either interface can be satisfied
    /// from a single registration.
    /// </summary>
    public async Task<IReadOnlyList<SeafoodItem>> DetectAsync(
        byte[] frameData,
        CancellationToken cancellationToken = default)
    {
        var dtos = await InferAsync(frameData, cancellationToken);
        int tmpId = 0;
        return dtos
            .Select(d => new SeafoodItem(
                new TrackingId(tmpId++),
                d.Label,
                d.Confidence,
                d.ToBoundingBox(),
                DateTime.UtcNow))
            .ToList()
            .AsReadOnly();
    }
}
