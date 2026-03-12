using System.Windows;
using System.Windows.Controls;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.App.Services;
using GlassFactory.BillTracker.App.ViewModels;

namespace GlassFactory.BillTracker.App.Views;

public partial class PrintBillsWindow : Window
{
    private readonly PrintBillsViewModel _viewModel;
    private PrintDialog? _selectedPrintDialog;

    public PrintBillsWindow(IReadOnlyList<OrderExportDto> orders, IPrintService printService)
    {
        _viewModel = new PrintBillsViewModel(orders ?? Array.Empty<OrderExportDto>(), printService);
        InitializeComponent();
        DataContext = _viewModel;
        PrinterTextBlock.Text = "未选择打印机";
    }

    private void SelectPrinterButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _selectedPrintDialog = dialog;
        PrinterTextBlock.Text = dialog.PrintQueue?.FullName ?? "默认打印机";
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Orders.Count == 0)
        {
            MessageBox.Show("请先选择要打印的订单", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var printDialog = _selectedPrintDialog ?? new PrintDialog();
        if (_selectedPrintDialog is null && printDialog.ShowDialog() != true)
        {
            return;
        }

        _viewModel.RefreshPreview();
        var document = _viewModel.PreviewDocument;

        if (document.Pages.Count == 0)
        {
            MessageBox.Show("没有可打印内容。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        printDialog.PrintDocument(document.DocumentPaginator, "GlassFactoryBillTracker_Bills");
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
