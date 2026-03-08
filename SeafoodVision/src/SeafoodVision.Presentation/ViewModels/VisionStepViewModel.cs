using System.Text.Json;
using System.Text.Json.Serialization;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Inspection.Pipeline.Models;

namespace SeafoodVision.Presentation.ViewModels;

/// <summary>
/// Wrapper around a single InspectionStep, deserializing
/// its JSON parameters into a strongly-typed, observable object.
/// </summary>
public sealed class VisionStepViewModel : ViewModelBase
{
    private readonly InspectionStep _step;
    private object _parameters;
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public VisionStepViewModel(InspectionStep step)
    {
        _step = step;
        _parameters = InstantiateParameters(step.StepType, step.ParametersJson);
    }

    /// <summary>Underlying model reference</summary>
    public InspectionStep Model => _step;

    public StepType Type => _step.StepType;

    public string Name => _step.StepType.ToString();

    /// <summary>
    /// Holds the polymorphic parameter class (e.g. ThresholdParams).
    /// Bound directly to the UI's ContentControl.
    /// </summary>
    public object Parameters
    {
        get => _parameters;
        set
        {
            if (SetField(ref _parameters, value))
            {
                SaveParametersToModel();
            }
        }
    }

    /// <summary>
    /// Optional 1-based order of the step whose output should be used as this step's
    /// primary input. Null = use the previous sequential step (default).
    /// </summary>
    public int? InputStepOrder
    {
        get => _step.InputStepOrder;
        set
        {
            if (_step.InputStepOrder != value)
            {
                _step.UpdateInputStepOrder(value);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Persists current parameter state back into the JSON entity property.
    /// Call this when a slide/textbox loses focus in the UI.
    /// </summary>
    public void SaveParametersToModel()
    {
        if (_parameters != null)
        {
            _step.UpdateParameters(JsonSerializer.Serialize(_parameters, _jsonOptions));
        }
    }

    private static object InstantiateParameters(StepType type, string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
                return CreateDefaultParams(type);

            return type switch
            {
                StepType.GrayConvert => JsonSerializer.Deserialize<GrayConvertParams>(json, _jsonOptions) ?? new GrayConvertParams(),
                StepType.ColorFilter => JsonSerializer.Deserialize<ColorFilterParams>(json, _jsonOptions) ?? new ColorFilterParams(),
                StepType.GaussianBlur => JsonSerializer.Deserialize<GaussianBlurParams>(json, _jsonOptions) ?? new GaussianBlurParams(),
                StepType.MedianBlur => JsonSerializer.Deserialize<MedianBlurParams>(json, _jsonOptions) ?? new MedianBlurParams(),
                StepType.Threshold => JsonSerializer.Deserialize<ThresholdParams>(json, _jsonOptions) ?? new ThresholdParams(),
                StepType.AdaptiveThreshold => JsonSerializer.Deserialize<AdaptiveThresholdParams>(json, _jsonOptions) ?? new AdaptiveThresholdParams(),
                StepType.Morphology => JsonSerializer.Deserialize<MorphologyParams>(json, _jsonOptions) ?? new MorphologyParams(),
                StepType.Canny => JsonSerializer.Deserialize<CannyParams>(json, _jsonOptions) ?? new CannyParams(),
                StepType.ContourFilter => JsonSerializer.Deserialize<ContourFilterParams>(json, _jsonOptions) ?? new ContourFilterParams(),
                StepType.BlobDetector => JsonSerializer.Deserialize<BlobDetectorParams>(json, _jsonOptions) ?? new BlobDetectorParams(),
                StepType.TemplateMatcher => JsonSerializer.Deserialize<TemplateMatcherParams>(json, _jsonOptions) ?? new TemplateMatcherParams(),
                StepType.DefectDetector => JsonSerializer.Deserialize<DefectDetectorParams>(json, _jsonOptions) ?? new DefectDetectorParams(),
                StepType.CropImage => JsonSerializer.Deserialize<CropParams>(json, _jsonOptions) ?? new CropParams(),
                StepType.SubtractImage => JsonSerializer.Deserialize<SubtractImageParams>(json, _jsonOptions) ?? new SubtractImageParams(),
                StepType.IntersectionRegion => JsonSerializer.Deserialize<IntersectionRegionParams>(json, _jsonOptions) ?? new IntersectionRegionParams(),
                StepType.GetRectangle => JsonSerializer.Deserialize<GetRectangleParams>(json, _jsonOptions) ?? new GetRectangleParams(),
                StepType.AddRegion => JsonSerializer.Deserialize<AddRegionParams>(json, _jsonOptions) ?? new AddRegionParams(),
                _ => new object()
            };
        }
        catch
        {
            return CreateDefaultParams(type);
        }
    }

    private static object CreateDefaultParams(StepType type)
    {
        return type switch
        {
            StepType.GrayConvert => new GrayConvertParams(),
            StepType.ColorFilter => new ColorFilterParams(),
            StepType.GaussianBlur => new GaussianBlurParams(),
            StepType.MedianBlur => new MedianBlurParams(),
            StepType.Threshold => new ThresholdParams(),
            StepType.AdaptiveThreshold => new AdaptiveThresholdParams(),
            StepType.Morphology => new MorphologyParams(),
            StepType.Canny => new CannyParams(),
            StepType.ContourFilter => new ContourFilterParams(),
            StepType.BlobDetector => new BlobDetectorParams(),
            StepType.TemplateMatcher => new TemplateMatcherParams(),
            StepType.DefectDetector => new DefectDetectorParams(),
            StepType.CropImage => new CropParams(),
            StepType.SubtractImage => new SubtractImageParams(),
            StepType.IntersectionRegion => new IntersectionRegionParams(),
            StepType.GetRectangle => new GetRectangleParams(),
            StepType.AddRegion => new AddRegionParams(),
            _ => new object()
        };
    }
}
