using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Printing;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.App.Services;
using GlassFactory.BillTracker.App.ViewModels;

namespace GlassFactory.BillTracker.App.Views;

public partial class PrintBillsWindow : Window
{
    private readonly PrintBillsViewModel _viewModel;
    private PrintDialog? _selectedPrintDialog;
    private PrintQueue? _selectedPrintQueue;
    private PrintTicket? _selectedPrintTicket;

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

        ApplyPrinterSelection(dialog);
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

        ApplyPrinterSelection(printDialog);

        _viewModel.RegeneratePreview(force: true);
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

    private void ApplyPrinterSelection(PrintDialog dialog)
    {
        _selectedPrintDialog = dialog;
        _selectedPrintQueue = dialog.PrintQueue;
        _selectedPrintTicket = dialog.PrintTicket;
        PrinterTextBlock.Text = _selectedPrintQueue?.FullName ?? "默认打印机";

        UpdatePrinterImageableArea(dialog);
    }

    private void UpdatePrinterImageableArea(PrintDialog dialog)
    {
        if (_selectedPrintQueue is null)
        {
            _viewModel.ClearPrinterImageableArea();
            Debug.WriteLine("Print scale: no selected printer queue; using default scale=100%.");
            return;
        }

        var originX = 0d;
        var originY = 0d;
        var extentWidth = 0d;
        var extentHeight = 0d;
        var fromCapabilities = false;

        try
        {
            var caps = _selectedPrintQueue.GetPrintCapabilities(_selectedPrintTicket);
            var imageable = caps?.PageImageableArea;
            if (imageable is not null && imageable.ExtentWidth > 0d && imageable.ExtentHeight > 0d)
            {
                originX = imageable.OriginWidth;
                originY = imageable.OriginHeight;
                extentWidth = imageable.ExtentWidth;
                extentHeight = imageable.ExtentHeight;
                fromCapabilities = true;
            }
            else
            {
                extentWidth = dialog.PrintableAreaWidth;
                extentHeight = dialog.PrintableAreaHeight;
                Debug.WriteLine($"Print scale warning: PageImageableArea missing for printer '{_selectedPrintQueue.FullName}', fallback to PrintDialog.PrintableAreaWidth/Height.");
            }
        }
        catch (Exception ex)
        {
            extentWidth = dialog.PrintableAreaWidth;
            extentHeight = dialog.PrintableAreaHeight;
            Debug.WriteLine($"Print scale warning: failed to query capabilities for printer '{_selectedPrintQueue.FullName}', fallback to PrintDialog printable area. Error={ex.Message}");
        }

        _viewModel.SetPrinterImageableArea(
            _selectedPrintQueue.FullName,
            fromCapabilities,
            originX,
            originY,
            extentWidth,
            extentHeight);

        Debug.WriteLine(
            $"Print scale printer profile: printer={_selectedPrintQueue.FullName}, fromCaps={fromCapabilities}, origin=({originX:F2},{originY:F2}), extent=({extentWidth:F2}x{extentHeight:F2})");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
