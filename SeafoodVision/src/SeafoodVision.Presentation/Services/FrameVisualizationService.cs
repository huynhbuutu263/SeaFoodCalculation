using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using SeafoodVision.Application.DTOs;
using SeafoodVision.Presentation.Models;

namespace SeafoodVision.Presentation.Services;

public interface IFrameVisualizationService
{
    /// <summary>
    /// Converts a domain frame visual DTO into a bitmap and UI overlays.
    /// </summary>
    (BitmapSource? frame, IReadOnlyList<DetectionOverlay> overlays) CreateVisuals(FrameVisualDto dto);
}

public sealed class FrameVisualizationService : IFrameVisualizationService
{
    private readonly ILogger<FrameVisualizationService> _logger;

    public FrameVisualizationService(ILogger<FrameVisualizationService> logger)
    {
        _logger = logger;
    }

    public (BitmapSource? frame, IReadOnlyList<DetectionOverlay> overlays) CreateVisuals(FrameVisualDto dto)
    {
        BitmapSource? bitmap = null;
        var overlays = new Collection<DetectionOverlay>();

        try
        {
            using var ms = new MemoryStream(dto.FrameBytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            bitmap = bmp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode frame bytes for visualization.");
        }

        foreach (var item in dto.Items)
        {
            var box = item.BoundingBox;
            overlays.Add(new DetectionOverlay
            {
                X = box.X,
                Y = box.Y,
                Width = box.Width,
                Height = box.Height,
                Confidence = item.Confidence,
                Label = item.Label
            });
        }

        return (bitmap, overlays);
    }
}

