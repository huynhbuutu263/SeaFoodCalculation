using OpenCvSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SeafoodVision.Inspection.Pipeline.Models;

/// <summary>
/// Parameter definitions for all supported StepTypes.
/// Inherit ParameterBase so WPF bindings instantly update Live Previews via PropertyChanged events.
/// </summary>

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

public class TemplateMatcherParams : ParameterBase
{
    private string _tp = ""; public string TemplatePath { get => _tp; set => SetField(ref _tp, value); }
    private TemplateMatchModes _m = TemplateMatchModes.CCoeffNormed; public TemplateMatchModes Method { get => _m; set => SetField(ref _m, value); }
    private double _mt = 0.8; public double MatchThreshold { get => _mt; set => SetField(ref _mt, value); }
}

public class DefectDetectorParams : ParameterBase
{
    private string _rp = ""; public string ReferencePath { get => _rp; set => SetField(ref _rp, value); }
    private int _s = 30; public int Sensitivity { get => _s; set => SetField(ref _s, value); }
    private double _mda = 50; public double MinDefectArea { get => _mda; set => SetField(ref _mda, value); }
}
