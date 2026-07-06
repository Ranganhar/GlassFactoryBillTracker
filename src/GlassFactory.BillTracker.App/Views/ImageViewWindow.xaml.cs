using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GlassFactory.BillTracker.App.Views;

public partial class ImageViewWindow : Window
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 0.1;

    private BitmapSource? _bitmap;
    private double _currentZoom = 1.0;
    private bool _isFitMode = true;

    public ImageViewWindow(string imagePath)
    {
        InitializeComponent();

        ImagePathText.Text = Path.GetFileName(imagePath);

        if (!File.Exists(imagePath))
        {
            MessageBox.Show("图片文件不存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        _bitmap = bitmap;
        PreviewImage.Source = bitmap;
        UpdateZoomText();

        Loaded += (_, _) => FitToWindow();
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        _isFitMode = false;
        ApplyZoom(_currentZoom + ZoomStep);
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        _isFitMode = false;
        ApplyZoom(_currentZoom - ZoomStep);
    }

    private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
    {
        _isFitMode = false;
        ApplyZoom(1.0);
    }

    private void FitButton_Click(object sender, RoutedEventArgs e)
    {
        _isFitMode = true;
        FitToWindow();
    }

    private void ImageScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isFitMode)
        {
            FitToWindow();
        }
    }

    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        _isFitMode = false;
        var step = e.Delta > 0 ? ZoomStep : -ZoomStep;
        var anchor = e.GetPosition(ImageScrollViewer);
        ApplyZoom(_currentZoom + step, anchor);
        e.Handled = true;
    }

    private void FitToWindow()
    {
        if (_bitmap is null)
        {
            return;
        }

        var viewportWidth = ImageScrollViewer.ViewportWidth;
        var viewportHeight = ImageScrollViewer.ViewportHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var scaleX = viewportWidth / _bitmap.PixelWidth;
        var scaleY = viewportHeight / _bitmap.PixelHeight;
        var fitScale = Math.Min(scaleX, scaleY);

        ApplyZoom(fitScale);
        ImageScrollViewer.ScrollToHorizontalOffset(0);
        ImageScrollViewer.ScrollToVerticalOffset(0);
    }

    private void ApplyZoom(double requestedZoom, Point? anchorInViewport = null)
    {
        var clampedZoom = Math.Clamp(requestedZoom, MinZoom, MaxZoom);
        if (Math.Abs(clampedZoom - _currentZoom) < 0.0001)
        {
            return;
        }

        var anchor = anchorInViewport ?? new Point(ImageScrollViewer.ViewportWidth / 2.0, ImageScrollViewer.ViewportHeight / 2.0);
        var previousZoom = _currentZoom;

        var anchorContentX = (ImageScrollViewer.HorizontalOffset + anchor.X) / previousZoom;
        var anchorContentY = (ImageScrollViewer.VerticalOffset + anchor.Y) / previousZoom;

        _currentZoom = clampedZoom;
        PreviewScaleTransform.ScaleX = _currentZoom;
        PreviewScaleTransform.ScaleY = _currentZoom;

        // Ensure scrollable dimensions are refreshed before adjusting offsets.
        ImageScrollViewer.UpdateLayout();

        var newHorizontalOffset = (anchorContentX * _currentZoom) - anchor.X;
        var newVerticalOffset = (anchorContentY * _currentZoom) - anchor.Y;

        ImageScrollViewer.ScrollToHorizontalOffset(Math.Max(0, newHorizontalOffset));
        ImageScrollViewer.ScrollToVerticalOffset(Math.Max(0, newVerticalOffset));

        UpdateZoomText();
    }

    private void UpdateZoomText()
    {
        var percent = (int)Math.Round(_currentZoom * 100);
        ZoomPercentText.Text = $"{percent}%";
    }
}
