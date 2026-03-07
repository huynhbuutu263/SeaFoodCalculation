using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SeafoodVision.Presentation.ViewModels;

namespace SeafoodVision.Presentation.Views;

/// <summary>
/// Interaction logic for VisionConfigDialog.xaml
/// </summary>
public partial class VisionConfigDialog : Window
{
    private double _zoomFactor = 1.0;
    private const double ZoomStep = 1.2;
    private const double MinZoom = 0.05;
    private const double MaxZoom = 20.0;

    public VisionConfigDialog(VisionConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Cleanly execute disposal handles if the window is closed directly by user
        Closed += (s, e) => viewModel.Dispose();

        // Keep zoom level text in sync when preview image changes
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(VisionConfigViewModel.PreviewFrame))
                UpdateZoomDisplay();
        };
    }

    // ── Zoom controls ─────────────────────────────────────────────────────────

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomFactor * ZoomStep);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomFactor / ZoomStep);

    private void ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is VisionConfigViewModel vm && vm.PreviewFrame is BitmapSource bmp)
        {
            double viewW = ImageScrollViewer.ViewportWidth;
            double viewH = ImageScrollViewer.ViewportHeight;
            if (viewW <= 0 || viewH <= 0 || bmp.PixelWidth <= 0 || bmp.PixelHeight <= 0)
            {
                SetZoom(1.0);
                return;
            }
            double fitZoom = Math.Min(viewW / bmp.PixelWidth, viewH / bmp.PixelHeight);
            SetZoom(fitZoom);
        }
        else
        {
            SetZoom(1.0);
        }
    }

    private void SetZoom(double zoom)
    {
        _zoomFactor = Math.Clamp(zoom, MinZoom, MaxZoom);
        ImageScaleTransform.ScaleX = _zoomFactor;
        ImageScaleTransform.ScaleY = _zoomFactor;
        UpdateZoomDisplay();
    }

    private void UpdateZoomDisplay()
    {
        ZoomLevelText.Text = $"{_zoomFactor * 100:F0}%";
    }

    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        double factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
        SetZoom(_zoomFactor * factor);
    }

    // ── Coordinate and RGB tracking ───────────────────────────────────────────

    private void PreviewImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not VisionConfigViewModel vm || vm.PreviewFrame is not BitmapSource bmp)
            return;

        // Mouse position is in the Image's layout space (before LayoutTransform).
        // LayoutTransform ScaleX/Y are applied by the layout system, so the Image's
        // ActualWidth/Height already reflect the unscaled pixel dimensions when Stretch=None.
        var pos = e.GetPosition(PreviewImage);

        // Convert to pixel coordinates
        int imgX = (int)Math.Floor(pos.X);
        int imgY = (int)Math.Floor(pos.Y);

        imgX = Math.Clamp(imgX, 0, bmp.PixelWidth - 1);
        imgY = Math.Clamp(imgY, 0, bmp.PixelHeight - 1);

        CoordText.Text = $"X:{imgX}  Y:{imgY}";

        var (r, g, b) = GetPixelColor(bmp, imgX, imgY);
        RgbText.Text = $"R:{r,3} G:{g,3} B:{b,3}";
        ColorSwatch.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    private void PreviewImage_MouseLeave(object sender, MouseEventArgs e)
    {
        CoordText.Text = "X:—  Y:—";
        RgbText.Text = "R:—  G:—  B:—";
        ColorSwatch.Background = null;
    }

    /// <summary>Samples a single pixel from a <see cref="BitmapSource"/>.</summary>
    private static (byte R, byte G, byte B) GetPixelColor(BitmapSource bmp, int x, int y)
    {
        try
        {
            // Normalise to BGRA32 for predictable byte layout
            var formatted = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            var cropped = new CroppedBitmap(formatted, new Int32Rect(x, y, 1, 1));
            var pixels = new byte[4]; // B G R A
            cropped.CopyPixels(pixels, 4, 0);
            return (R: pixels[2], G: pixels[1], B: pixels[0]);
        }
        catch
        {
            return (0, 0, 0);
        }
    }
}
