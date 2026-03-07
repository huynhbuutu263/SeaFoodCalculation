using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Interfaces;

namespace SeafoodVision.Inspection.Cache;

/// <summary>
/// <see cref="IRecipeCache"/> implementation backed by <see cref="IMemoryCache"/>.
/// Each camera's active recipe is stored under the key <c>"recipe:active:{cameraId}"</c>
/// with a 5-minute sliding expiration. Entries are also removed on demand via
/// <see cref="Invalidate"/> when the operator saves a new recipe configuration.
/// </summary>
public sealed class RecipeCacheService : IRecipeCache
{
    private readonly IRecipeRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RecipeCacheService> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public RecipeCacheService(
        IRecipeRepository repository,
        IMemoryCache cache,
        ILogger<RecipeCacheService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<InspectionRecipe?> GetActiveAsync(string cameraId, CancellationToken ct = default)
    {
        var key = CacheKey(cameraId);

        if (_cache.TryGetValue(key, out InspectionRecipe? cached))
        {
            _logger.LogDebug("Recipe cache hit for camera {CameraId}", cameraId);
            return cached;
        }

        _logger.LogDebug("Recipe cache miss for camera {CameraId} — querying DB", cameraId);
        var recipes = await _repository.GetActiveAsync(cameraId, ct);
        var recipe = recipes.OrderByDescending(r => r.UpdatedAt).FirstOrDefault();

        // Cache even a null result so we don't hammer the DB on every frame when no recipe is active
        _cache.Set(key, recipe, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheDuration
        });

        if (recipe is null)
            _logger.LogInformation("No active recipe found for camera {CameraId} — inspection pass-through", cameraId);
        else
            _logger.LogInformation("Cached active recipe '{Name}' (Id={Id}) for camera {CameraId}", recipe.Name, recipe.Id, cameraId);

        return recipe;
    }

    /// <inheritdoc/>
    public void Invalidate(string cameraId)
    {
        _cache.Remove(CacheKey(cameraId));
        _logger.LogInformation("Recipe cache invalidated for camera {CameraId}", cameraId);
    }

    private static string CacheKey(string cameraId) => $"recipe:active:{cameraId}";
}
