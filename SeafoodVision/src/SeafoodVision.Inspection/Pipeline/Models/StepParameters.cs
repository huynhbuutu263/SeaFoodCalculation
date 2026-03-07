using OpenCvSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SeafoodVision.Inspection.Pipeline.Models;

/// <summary>
/// Parameter definitions for all supported StepTypes.
/// Inherit ParameterBase so WPF bindings instantly update Live Previews via PropertyChanged events.
/// </summary>

/// <summary>
/// Marker interface for dual-input step parameters that carry a reference to a secondary
/// pipeline step whose output is used as the second Mat input (e.g. subtract, intersect).
/// </summary>
public interface IHasSecondaryInput
{
    int SecondaryInputStepOrder { get; }
}

public abstract class ParameterBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class GrayConvertParams : ParameterBase { }

public class ColorFilterParams : ParameterBase
{
    private int _hMin = 0; public int HMin { get => _hMin; set => SetField(ref _hMin, value); }
    private int _hMax = 179; public int HMax { get => _hMax; set => SetField(ref _hMax, value); }
    private int _sMin = 0; public int SMin { get => _sMin; set => SetField(ref _sMin, value); }
    private int _sMax = 255; public int SMax { get => _sMax; set => SetField(ref _sMax, value); }
    private int _vMin = 0; public int VMin { get => _vMin; set => SetField(ref _vMin, value); }
    private int _vMax = 255; public int VMax { get => _vMax; set => SetField(ref _vMax, value); }
}

public class GaussianBlurParams : ParameterBase
{
    private int _kw = 5; public int KernelWidth { get => _kw; set => SetField(ref _kw, value); }
    private int _kh = 5; public int KernelHeight { get => _kh; set => SetField(ref _kh, value); }
    private double _sx = 0; public double SigmaX { get => _sx; set => SetField(ref _sx, value); }
}

public class MedianBlurParams : ParameterBase
{
    private int _ks = 5; public int KernelSize { get => _ks; set => SetField(ref _ks, value); }
}

public class ThresholdParams : ParameterBase
{
    private double _tv = 127; public double ThreshValue { get => _tv; set => SetField(ref _tv, value); }
    private double _mv = 255; public double MaxValue { get => _mv; set => SetField(ref _mv, value); }
    private ThresholdTypes _tt = ThresholdTypes.Binary; public ThresholdTypes ThreshType { get => _tt; set => SetField(ref _tt, value); }
}

public class AdaptiveThresholdParams : ParameterBase
{
    private double _mv = 255; public double MaxValue { get => _mv; set => SetField(ref _mv, value); }
    private AdaptiveThresholdTypes _m = AdaptiveThresholdTypes.GaussianC; public AdaptiveThresholdTypes Method { get => _m; set => SetField(ref _m, value); }
    private ThresholdTypes _tt = ThresholdTypes.Binary; public ThresholdTypes ThreshType { get => _tt; set => SetField(ref _tt, value); }
    private int _bs = 11; public int BlockSize { get => _bs; set => SetField(ref _bs, value); }
    private double _c = 2; public double C { get => _c; set => SetField(ref _c, value); }
}

public class MorphologyParams : ParameterBase
{
    private MorphTypes _op = MorphTypes.Open; public MorphTypes Operation { get => _op; set => SetField(ref _op, value); }
    private int _ks = 3; public int KernelSize { get => _ks; set => SetField(ref _ks, value); }
    private int _it = 1; public int Iterations { get => _it; set => SetField(ref _it, value); }
}

public class CannyParams : ParameterBase
{
    private double _t1 = 100; public double Threshold1 { get => _t1; set => SetField(ref _t1, value); }
    private double _t2 = 200; public double Threshold2 { get => _t2; set => SetField(ref _t2, value); }
    private int _ap = 3; public int ApertureSize { get => _ap; set => SetField(ref _ap, value); }
}

public class ContourFilterParams : ParameterBase
{
    private double _mina = 100; public double MinArea { get => _mina; set => SetField(ref _mina, value); }
    private double _maxa = 100000; public double MaxArea { get => _maxa; set => SetField(ref _maxa, value); }
    private double _minc = 0.0; public double MinCircularity { get => _minc; set => SetField(ref _minc, value); }
    private double _maxc = 1.0; public double MaxCircularity { get => _maxc; set => SetField(ref _maxc, value); }
    private double _minar = 0.0; public double MinAspectRatio { get => _minar; set => SetField(ref _minar, value); }
    private double _maxar = 100.0; public double MaxAspectRatio { get => _maxar; set => SetField(ref _maxar, value); }
}

public class BlobDetectorParams : ParameterBase
{
    private double _mina = 100; public double MinArea { get => _mina; set => SetField(ref _mina, value); }
    private double _maxa = 100000; public double MaxArea { get => _maxa; set => SetField(ref _maxa, value); }
    private double _minc = 0.0; public double MinCircularity { get => _minc; set => SetField(ref _minc, value); }
    private bool _fbc = true; public bool FilterByColor { get => _fbc; set => SetField(ref _fbc, value); }
    private int _bc = 255; public int BlobColor { get => _bc; set => SetField(ref _bc, value); }
}

public class TemplateMatcherParams : ParameterBase, IHasSecondaryInput
{
    private string _tp = ""; public string TemplatePath { get => _tp; set => SetField(ref _tp, value); }
    private TemplateMatchModes _m = TemplateMatchModes.CCoeffNormed; public TemplateMatchModes Method { get => _m; set => SetField(ref _m, value); }
    private double _mt = 0.8; public double MatchThreshold { get => _mt; set => SetField(ref _mt, value); }
    private int _maxMatches = 1; public int MaxMatches { get => _maxMatches; set => SetField(ref _maxMatches, value); }
    private double _nmsThreshold = 0.3; public double NMSThreshold { get => _nmsThreshold; set => SetField(ref _nmsThreshold, value); }
    /// <summary>
    /// When > 0, the output of the referenced step is used as the template image instead of
    /// loading from <see cref="TemplatePath"/>. Typically references a <c>TemplateMatchRegion</c>
    /// step that outputs the cropped template image.
    /// </summary>
    private int _tso = 0; public int TemplateStepOrder { get => _tso; set => SetField(ref _tso, value); }
    // IHasSecondaryInput: step order 0 = "not set" (use TemplatePath instead)
    int IHasSecondaryInput.SecondaryInputStepOrder => TemplateStepOrder;
}

public class DefectDetectorParams : ParameterBase
{
    private string _rp = ""; public string ReferencePath { get => _rp; set => SetField(ref _rp, value); }
    private int _s = 30; public int Sensitivity { get => _s; set => SetField(ref _s, value); }
    private double _mda = 50; public double MinDefectArea { get => _mda; set => SetField(ref _mda, value); }
}

// ── Region / Image-manipulation parameters ───────────────────────────────

public class CropParams : ParameterBase, IHasSecondaryInput
{
    private int _x = 0; public int X { get => _x; set => SetField(ref _x, value); }
    private int _y = 0; public int Y { get => _y; set => SetField(ref _y, value); }
    private int _w = 100; public int Width { get => _w; set => SetField(ref _w, value); }
    private int _h = 100; public int Height { get => _h; set => SetField(ref _h, value); }
    /// <summary>
    /// When > 0, the bounding rectangle of the non-zero pixels in the referenced step's output
    /// is used as the crop region, overriding <see cref="X"/>, <see cref="Y"/>,
    /// <see cref="Width"/> and <see cref="Height"/>.
    /// </summary>
    private int _rso = 0; public int RegionStepOrder { get => _rso; set => SetField(ref _rso, value); }
    // IHasSecondaryInput: step order 0 = "not set" (use X/Y/W/H instead)
    int IHasSecondaryInput.SecondaryInputStepOrder => RegionStepOrder;
}

public class SubtractImageParams : ParameterBase, IHasSecondaryInput
{
    private int _si = 1; public int SecondaryInputStepOrder { get => _si; set => SetField(ref _si, value); }
}

public class IntersectionRegionParams : ParameterBase, IHasSecondaryInput
{
    private int _si = 1; public int SecondaryInputStepOrder { get => _si; set => SetField(ref _si, value); }
}

public class GetRectangleParams : ParameterBase { }

public class TemplateMatchRegionParams : ParameterBase
{
    private int _tx = 0; public int TemplateX { get => _tx; set => SetField(ref _tx, value); }
    private int _ty = 0; public int TemplateY { get => _ty; set => SetField(ref _ty, value); }
    private int _tw = 50; public int TemplateWidth { get => _tw; set => SetField(ref _tw, value); }
    private int _th = 50; public int TemplateHeight { get => _th; set => SetField(ref _th, value); }
    private TemplateMatchModes _m = TemplateMatchModes.CCoeffNormed; public TemplateMatchModes Method { get => _m; set => SetField(ref _m, value); }
    private double _mt = 0.8; public double MatchThreshold { get => _mt; set => SetField(ref _mt, value); }
    /// <summary>
    /// Optional name of a recipe ROI whose bounds (relative to the current ROI crop) are used
    /// as the template region.  When non-empty this takes precedence over the manual X/Y/W/H values.
    /// </summary>
    private string _roiSourceName = ""; public string RoiSourceName { get => _roiSourceName; set => SetField(ref _roiSourceName, value); }
}
