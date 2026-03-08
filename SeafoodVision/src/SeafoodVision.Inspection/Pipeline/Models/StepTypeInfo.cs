using SeafoodVision.Domain.Enums;

namespace SeafoodVision.Inspection.Pipeline.Models;

/// <summary>
/// Describes the image-channel contract of a pipeline step: what it outputs and what it
/// requires as its primary input.  Used by the UI to gray out incompatible input sources.
/// </summary>
public enum StepImageType
{
    /// <summary>Any channel count is accepted / produced.</summary>
    Any = 0,
    /// <summary>Single-channel grayscale image.</summary>
    Grayscale = 1,
    /// <summary>Single-channel binary (0/255) mask.</summary>
    Binary = 2,
    /// <summary>Three-channel BGR colour image.</summary>
    Color = 3
}

/// <summary>
/// Static lookup table that returns the <see cref="StepImageType"/> that a given
/// <see cref="StepType"/> expects as input and produces as output.
/// </summary>
public static class StepTypeInfo
{
    /// <summary>Returns the image type this step requires as its primary input.</summary>
    public static StepImageType RequiredInput(StepType type) => type switch
    {
        StepType.GrayConvert          => StepImageType.Any,
        StepType.ColorFilter          => StepImageType.Color,
        StepType.GaussianBlur         => StepImageType.Any,
        StepType.MedianBlur           => StepImageType.Any,
        StepType.Threshold            => StepImageType.Grayscale,
        StepType.AdaptiveThreshold    => StepImageType.Grayscale,
        StepType.Morphology           => StepImageType.Any,
        StepType.Canny                => StepImageType.Grayscale,
        StepType.ContourFilter        => StepImageType.Binary,
        StepType.BlobDetector         => StepImageType.Binary,
        StepType.TemplateMatcher      => StepImageType.Color,
        StepType.DefectDetector       => StepImageType.Color,
        StepType.CropImage            => StepImageType.Any,
        StepType.SubtractImage        => StepImageType.Any,
        StepType.IntersectionRegion   => StepImageType.Binary,
        StepType.GetRectangle         => StepImageType.Binary,
        StepType.AddRegion            => StepImageType.Color,
        _                             => StepImageType.Any
    };

    /// <summary>Returns the image type this step produces as its output.</summary>
    public static StepImageType ProducedOutput(StepType type) => type switch
    {
        StepType.GrayConvert          => StepImageType.Grayscale,
        StepType.ColorFilter          => StepImageType.Binary,
        StepType.GaussianBlur         => StepImageType.Any,      // passes through channels
        StepType.MedianBlur           => StepImageType.Any,
        StepType.Threshold            => StepImageType.Binary,
        StepType.AdaptiveThreshold    => StepImageType.Binary,
        StepType.Morphology           => StepImageType.Any,
        StepType.Canny                => StepImageType.Binary,
        StepType.ContourFilter        => StepImageType.Binary,
        StepType.BlobDetector         => StepImageType.Binary,
        StepType.TemplateMatcher      => StepImageType.Color,
        StepType.DefectDetector       => StepImageType.Binary,
        StepType.CropImage            => StepImageType.Any,
        StepType.SubtractImage        => StepImageType.Any,
        StepType.IntersectionRegion   => StepImageType.Binary,
        StepType.GetRectangle         => StepImageType.Color,
        StepType.AddRegion            => StepImageType.Color,
        _                             => StepImageType.Any
    };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="outputType"/> is a compatible source for a
    /// step that requires <paramref name="requiredType"/> as input.
    /// <see cref="StepImageType.Any"/> is always compatible in both directions.
    /// </summary>
    public static bool IsCompatible(StepImageType outputType, StepImageType requiredType)
    {
        if (outputType == StepImageType.Any || requiredType == StepImageType.Any) return true;
        return outputType == requiredType;
    }
}
