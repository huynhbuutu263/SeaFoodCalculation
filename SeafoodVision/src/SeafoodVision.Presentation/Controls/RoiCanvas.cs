using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Presentation.Controls;

/// <summary>
/// A canvas overlay for drawing normalised [0,1] <see cref="RegionOfInterest"/> 
/// (Rectangle or Polygon) on top of a reference frame.
/// Correctly accounts for <c>Stretch="Uniform"</c> letterboxing so that drawn regions
/// line up with the underlying image pixels regardless of the container aspect ratio.
/// </summary>
public class RoiCanvas : Canvas
{
    // ── Dependency Properties ──────────────────────────────────────────────────

    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.Register(nameof(Region), typeof(RegionOfInterest), typeof(RoiCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRegionChanged));

    public RegionOfInterest? Region
    {
        get => (RegionOfInterest?)GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
    }

    public static readonly DependencyProperty ActiveShapeTypeProperty =
        DependencyProperty.Register(nameof(ActiveShapeType), typeof(RegionType), typeof(RoiCanvas),
            new PropertyMetadata(RegionType.Rectangle, OnActiveShapeTypeChanged));

    public RegionType ActiveShapeType
    {
        get => (RegionType)GetValue(ActiveShapeTypeProperty);
        set => SetValue(ActiveShapeTypeProperty, value);
    }

    /// <summary>
    /// The source image being overlaid.  When set, the canvas corrects for
    /// <c>Stretch="Uniform"</c> letterboxing so normalised coordinates map onto
    /// the actual rendered image pixels rather than the full canvas area.
    /// </summary>
    public static readonly DependencyProperty ReferenceFrameProperty =
        DependencyProperty.Register(nameof(ReferenceFrame), typeof(BitmapSource), typeof(RoiCanvas),
            new PropertyMetadata(null, OnReferenceFrameChanged));

    public BitmapSource? ReferenceFrame
    {
        get => (BitmapSource?)GetValue(ReferenceFrameProperty);
        set => SetValue(ReferenceFrameProperty, value);
    }

    // ── Internal State ────────────────────────────────────────────────────────

    private bool _isDrawing;
    private Point _drawStart;
    
    // UI elements
    private readonly Rectangle _rectShape;
    private readonly Polygon _polyShape;
    private readonly List<Point> _polygonPoints = new();

    public RoiCanvas()
    {
        Background = Brushes.Transparent; // Needs background to catch mouse events

        _rectShape = new Rectangle
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(50, 0, 255, 255)),
            Visibility = Visibility.Hidden
        };
        
        _polyShape = new Polygon
        {
            Stroke = Brushes.Magenta,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(50, 255, 0, 255)),
            Visibility = Visibility.Hidden
        };

        Children.Add(_rectShape);
        Children.Add(_polyShape);
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private static void OnRegionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RoiCanvas canvas) canvas.RenderRegion();
    }

    private static void OnActiveShapeTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RoiCanvas canvas)
        {
            // Reset state when switching modes
            canvas._isDrawing = false;
            canvas._polygonPoints.Clear();
            canvas.RenderRegion();
        }
    }

    private static void OnReferenceFrameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RoiCanvas canvas) canvas.RenderRegion();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        RenderRegion();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var pos = e.GetPosition(this);
        
        // Only accept clicks within the actual rendered image area
        var (imgX, imgY, imgW, imgH) = GetImageRenderBounds();
        if (imgW <= 0 || imgH <= 0) return;

        if (ActiveShapeType == RegionType.Rectangle)
        {
            // Start tracing new rectangle
            _isDrawing = true;
            _drawStart = pos;
            _rectShape.Visibility = Visibility.Visible;
            _polyShape.Visibility = Visibility.Collapsed;
            
            SetLeft(_rectShape, pos.X);
            SetTop(_rectShape, pos.Y);
            _rectShape.Width = 0;
            _rectShape.Height = 0;
            
            CaptureMouse();
        }
        else if (ActiveShapeType == RegionType.Polygon)
        {
            if (e.ClickCount == 2 && _polygonPoints.Count >= 3)
            {
                // Double click completes the polygon
                _isDrawing = false;
                CommitPolygon();
                return;
            }

            if (!_isDrawing)
            {
                _isDrawing = true;
                _polygonPoints.Clear();
                _rectShape.Visibility = Visibility.Collapsed;
                _polyShape.Visibility = Visibility.Visible;
            }

            _polygonPoints.Add(pos);
            _polyShape.Points = new PointCollection(_polygonPoints);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDrawing) return;

        var pos = e.GetPosition(this);
        var (imgX, imgY, imgW, imgH) = GetImageRenderBounds();

        if (ActiveShapeType == RegionType.Rectangle && IsMouseCaptured)
        {
            double x = Math.Min(pos.X, _drawStart.X);
            double y = Math.Min(pos.Y, _drawStart.Y);
            double w = Math.Abs(pos.X - _drawStart.X);
            double h = Math.Abs(pos.Y - _drawStart.Y);

            // Clamp to image render bounds
            double clampLeft = imgW > 0 ? imgX : 0;
            double clampTop  = imgH > 0 ? imgY : 0;
            double clampRight  = imgW > 0 ? imgX + imgW : ActualWidth;
            double clampBottom = imgH > 0 ? imgY + imgH : ActualHeight;

            x = Math.Max(clampLeft, x);
            y = Math.Max(clampTop, y);
            w = Math.Min(clampRight - x, w);
            h = Math.Min(clampBottom - y, h);

            SetLeft(_rectShape, x);
            SetTop(_rectShape, y);
            _rectShape.Width = Math.Max(0, w);
            _rectShape.Height = Math.Max(0, h);
        }
        else if (ActiveShapeType == RegionType.Polygon && _polygonPoints.Count > 0)
        {
            // Live preview line from last point to current mouse pos
            var pts = new List<Point>(_polygonPoints) { pos };
            _polyShape.Points = new PointCollection(pts);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        
        if (ActiveShapeType == RegionType.Rectangle && _isDrawing && IsMouseCaptured)
        {
            ReleaseMouseCapture();
            _isDrawing = false;
            CommitRectangle();
        }
    }

    // ── Image Render Bounds ────────────────────────────────────────────────────

    /// <summary>
    /// Computes the (X, Y, Width, Height) of the image's actual rendered area inside
    /// this canvas, accounting for <c>Stretch="Uniform"</c> letterboxing.
    /// When <see cref="ReferenceFrame"/> is null the full canvas area is returned.
    /// </summary>
    private (double X, double Y, double Width, double Height) GetImageRenderBounds()
    {
        double canvasW = ActualWidth;
        double canvasH = ActualHeight;

        if (canvasW <= 0 || canvasH <= 0)
            return (0, 0, canvasW, canvasH);

        var frame = ReferenceFrame;
        if (frame == null || frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
            return (0, 0, canvasW, canvasH);

        double imgAspect = (double)frame.PixelWidth / frame.PixelHeight;
        double containerAspect = canvasW / canvasH;

        double renderW, renderH, offsetX, offsetY;
        if (containerAspect > imgAspect)
        {
            // Pillar-box: empty space left and right
            renderH = canvasH;
            renderW = renderH * imgAspect;
            offsetX = (canvasW - renderW) / 2.0;
            offsetY = 0;
        }
        else
        {
            // Letter-box: empty space top and bottom
            renderW = canvasW;
            renderH = renderW / imgAspect;
            offsetX = 0;
            offsetY = (canvasH - renderH) / 2.0;
        }

        return (offsetX, offsetY, renderW, renderH);
    }

    /// <summary>
    /// Converts a canvas-space point to a normalised [0,1] coordinate relative to the
    /// actual rendered image bounds (corrects for letterboxing).
    /// </summary>
    private System.Drawing.PointF ToNormalised(Point canvasPoint)
    {
        var (imgX, imgY, imgW, imgH) = GetImageRenderBounds();
        if (imgW <= 0 || imgH <= 0) return new((float)(canvasPoint.X / ActualWidth), (float)(canvasPoint.Y / ActualHeight));

        float nx = (float)Math.Clamp((canvasPoint.X - imgX) / imgW, 0.0, 1.0);
        float ny = (float)Math.Clamp((canvasPoint.Y - imgY) / imgH, 0.0, 1.0);
        return new(nx, ny);
    }

    /// <summary>
    /// Converts a normalised [0,1] coordinate to a canvas-space point, accounting for letterboxing.
    /// </summary>
    private Point ToCanvasPoint(System.Drawing.PointF normalised)
    {
        var (imgX, imgY, imgW, imgH) = GetImageRenderBounds();
        return new Point(imgX + normalised.X * imgW, imgY + normalised.Y * imgH);
    }

    // ── Committing to Model ───────────────────────────────────────────────────

    private void CommitRectangle()
    {
        if (_rectShape.Width < 5 || _rectShape.Height < 5) return; // Too small
        
        double left = GetLeft(_rectShape);
        double top  = GetTop(_rectShape);
        
        var tlNorm = ToNormalised(new Point(left, top));
        var brNorm = ToNormalised(new Point(left + _rectShape.Width, top + _rectShape.Height));

        Region = RegionOfInterest.FromRectangle(tlNorm, brNorm);
    }

    private void CommitPolygon()
    {
        var normalizedPoints = _polygonPoints
            .Select(p => ToNormalised(p))
            .ToList();

        Region = RegionOfInterest.FromPolygon(normalizedPoints);
        _polyShape.Points = new PointCollection(_polygonPoints);
    }

    // ── Rendering from Model ──────────────────────────────────────────────────

    private void RenderRegion()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        if (Region is null)
        {
            _rectShape.Visibility = Visibility.Collapsed;
            _polyShape.Visibility = Visibility.Collapsed;
            return;
        }

        if (Region.RegionType == RegionType.Rectangle)
        {
            _polyShape.Visibility = Visibility.Collapsed;
            _rectShape.Visibility = Visibility.Visible;
            
            var tl = ToCanvasPoint(Region.Points[0]);
            var br = ToCanvasPoint(Region.Points[1]);
            
            SetLeft(_rectShape, tl.X);
            SetTop(_rectShape, tl.Y);
            _rectShape.Width  = Math.Max(0, br.X - tl.X);
            _rectShape.Height = Math.Max(0, br.Y - tl.Y);
        }
        else if (Region.RegionType == RegionType.Polygon)
        {
            _rectShape.Visibility = Visibility.Collapsed;
            _polyShape.Visibility = Visibility.Visible;
            
            _polygonPoints.Clear();
            foreach (var p in Region.Points)
                _polygonPoints.Add(ToCanvasPoint(p));

            _polyShape.Points = new PointCollection(_polygonPoints);
        }
    }
    
    // Note: To Clear, caller just sets Region = null (since it's TwoWay bound).
}
