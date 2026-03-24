using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GlassFactory.BillTracker.App.Views;

public partial class ImageViewWindow : Window
{
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

        PreviewImage.Source = bitmap;
    }
}
