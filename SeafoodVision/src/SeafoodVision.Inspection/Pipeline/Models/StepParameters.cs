using OpenCvSharp;

namespace SeafoodVision.Inspection.Pipeline.Models;

/// <summary>
/// Parameter definitions for all supported StepTypes.
/// These serialize cleanly to JSON for the InspectionStep.ParametersJson column.
/// </summary>

public class GrayConvertParams { }

public class ColorFilterParams
{
    public int HMin { get; set; } = 0; public int HMax { get; set; } = 179;
    public int SMin { get; set; } = 0; public int SMax { get; set; } = 255;
    public int VMin { get; set; } = 0; public int VMax { get; set; } = 255;
}

public class GaussianBlurParams
{
    public int KernelWidth { get; set; } = 5;
    public int KernelHeight { get; set; } = 5;
    public double SigmaX { get; set; } = 0.0;
}

public class MedianBlurParams
{
    public int KernelSize { get; set; } = 5;
}

public class ThresholdParams
{
    public double ThreshValue { get; set; } = 127;
    public double MaxValue { get; set; } = 255;
    public ThresholdTypes ThreshType { get; set; } = ThresholdTypes.Binary;
}

public class AdaptiveThresholdParams
{
    public double MaxValue { get; set; } = 255;
    public AdaptiveThresholdTypes Method { get; set; } = AdaptiveThresholdTypes.GaussianC;
    public ThresholdTypes ThreshType { get; set; } = ThresholdTypes.Binary;
    public int BlockSize { get; set; } = 11;
    public double C { get; set; } = 2;
}

public class MorphologyParams
{
    public MorphTypes Operation { get; set; } = MorphTypes.Open;
    public int KernelSize { get; set; } = 3;
    public int Iterations { get; set; } = 1;
}

public class CannyParams
{
    public double Threshold1 { get; set; } = 100;
    public double Threshold2 { get; set; } = 200;
    public int ApertureSize { get; set; } = 3;
}

public class ContourFilterParams
{
    public double MinArea { get; set; } = 100;
    public double MaxArea { get; set; } = 100000;
    public double MinCircularity { get; set; } = 0.0;
    public double MaxCircularity { get; set; } = 1.0;
    public double MinAspectRatio { get; set; } = 0.0;
    public double MaxAspectRatio { get; set; } = 100.0;
}

public class BlobDetectorParams
{
    public double MinArea { get; set; } = 100;
    public double MaxArea { get; set; } = 100000;
    public double MinCircularity { get; set; } = 0.0;
    public bool FilterByColor { get; set; } = true;
    public int BlobColor { get; set; } = 255;
}

public class TemplateMatcherParams
{
    public string TemplatePath { get; set; } = "";
    public TemplateMatchModes Method { get; set; } = TemplateMatchModes.CCoeffNormed;
    public double MatchThreshold { get; set; } = 0.8;
}

public class DefectDetectorParams
{
    public string ReferencePath { get; set; } = "";
    public int Sensitivity { get; set; } = 30;
    public double MinDefectArea { get; set; } = 50;
}
