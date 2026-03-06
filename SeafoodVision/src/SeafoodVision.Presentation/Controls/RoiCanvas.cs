using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Presentation.Controls;

/// <summary>
/// A canvas overlay for drawing normalises [0,1] <see cref="RegionOfInterest"/> 
/// (Rectangle or Polygon) on top of a reference frame.
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

        if (ActiveShapeType == RegionType.Rectangle && IsMouseCaptured)
        {
            double x = Math.Min(pos.X, _drawStart.X);
            double y = Math.Min(pos.Y, _drawStart.Y);
            double w = Math.Abs(pos.X - _drawStart.X);
            double h = Math.Abs(pos.Y - _drawStart.Y);

            // Clamp to canvas bounds
            x = Math.Max(0, x);
            y = Math.Max(0, y);
            w = Math.Min(ActualWidth - x, w);
            h = Math.Min(ActualHeight - y, h);

            SetLeft(_rectShape, x);
            SetTop(_rectShape, y);
            _rectShape.Width = w;
            _rectShape.Height = h;
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

    // ── Committing to Model ───────────────────────────────────────────────────

    private void CommitRectangle()
    {
        if (_rectShape.Width < 5 || _rectShape.Height < 5) return; // Too small
        
        double left = GetLeft(_rectShape);
        double top = GetTop(_rectShape);
        
        var normalizedPoints = new[]
        {
            new System.Drawing.PointF((float)(left / ActualWidth), (float)(top / ActualHeight)),
            new System.Drawing.PointF((float)((left + _rectShape.Width) / ActualWidth), (float)((top + _rectShape.Height) / ActualHeight))
        };

        Region = RegionOfInterest.FromRectangle(normalizedPoints[0], normalizedPoints[1]);
    }

    private void CommitPolygon()
    {
        var normalizedPoints = _polygonPoints.Select(p => 
            new System.Drawing.PointF((float)(p.X / ActualWidth), (float)(p.Y / ActualHeight))
        ).ToList();

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
            
            var tl = Region.Points[0];
            var br = Region.Points[1];
            
            SetLeft(_rectShape, tl.X * ActualWidth);
            SetTop(_rectShape, tl.Y * ActualHeight);
            _rectShape.Width = (br.X - tl.X) * ActualWidth;
            _rectShape.Height = (br.Y - tl.Y) * ActualHeight;
        }
        else if (Region.RegionType == RegionType.Polygon)
        {
            _rectShape.Visibility = Visibility.Collapsed;
            _polyShape.Visibility = Visibility.Visible;
            
            _polygonPoints.Clear();
            foreach (var p in Region.Points)
            {
                _polygonPoints.Add(new Point(p.X * ActualWidth, p.Y * ActualHeight));
            }
            _polyShape.Points = new PointCollection(_polygonPoints);
        }
    }
    
    // Note: To Clear, caller just sets Region = null (since it's TwoWay bound).
}
