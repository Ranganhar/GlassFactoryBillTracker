using System.Windows;
using GlassFactory.BillTracker.App.ViewModels.Rows;

namespace GlassFactory.BillTracker.App.Views;

public partial class SampleBlockManagementWindow : Window
{
    public SampleBlockManagementWindow()
    {
        InitializeComponent();
    }

    private void AttachmentListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (AttachmentListBox.SelectedItem is not ManagedAttachmentViewModel att || string.IsNullOrWhiteSpace(att.AbsolutePath)) return;
        new ImageViewWindow(att.AbsolutePath) { Owner = this }.ShowDialog();
    }
}
