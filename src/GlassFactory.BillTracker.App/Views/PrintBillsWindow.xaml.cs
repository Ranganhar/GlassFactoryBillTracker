using System.Windows;
using System.Windows.Controls;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.App.Services;

namespace GlassFactory.BillTracker.App.Views;

public partial class PrintBillsWindow : Window
{
    private readonly IReadOnlyList<OrderExportDto> _orders;
    private readonly IPrintService _printService;
    private PrintDialog? _selectedPrintDialog;

    public PrintBillsWindow(IReadOnlyList<OrderExportDto> orders, IPrintService printService)
    {
        InitializeComponent();
        _orders = orders;
        _printService = printService;
        OrdersListBox.ItemsSource = orders;
        PrinterTextBlock.Text = "未选择打印机";
    }

    private void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = (TemplateComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        DotMatrixHeightComboBox.IsEnabled = string.Equals(tag, "DotMatrix", StringComparison.OrdinalIgnoreCase);
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
        if (_orders.Count == 0)
        {
            MessageBox.Show("没有可打印订单。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var printDialog = _selectedPrintDialog ?? new PrintDialog();
        if (_selectedPrintDialog is null && printDialog.ShowDialog() != true)
        {
            return;
        }

        var options = BuildOptions();
        var document = options.TemplateKind == PrintTemplateKind.DotMatrix
            ? _printService.RenderDotMatrixTriplicate(_orders, options)
            : _printService.RenderA4(_orders, options);

        printDialog.PrintDocument(document.DocumentPaginator, "GlassFactoryBillTracker_Bills");
        DialogResult = true;
        Close();
    }

    private PrintBillOptions BuildOptions()
    {
        var templateTag = (TemplateComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var heightTag = (DotMatrixHeightComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        var templateKind = string.Equals(templateTag, "A4", StringComparison.OrdinalIgnoreCase)
            ? PrintTemplateKind.A4
            : PrintTemplateKind.DotMatrix;

        var heightMode = heightTag switch
        {
            "Full" => DotMatrixHeightMode.Full,
            "Half" => DotMatrixHeightMode.Half,
            _ => DotMatrixHeightMode.Third
        };

        return new PrintBillOptions
        {
            HeaderText = string.IsNullOrWhiteSpace(HeaderTextBox.Text) ? "亿达夹丝玻璃" : HeaderTextBox.Text.Trim(),
            UseCustomerPhone = UseCustomerPhoneCheckBox.IsChecked == true,
            CustomPhone = string.IsNullOrWhiteSpace(CustomPhoneTextBox.Text) ? null : CustomPhoneTextBox.Text.Trim(),
            TemplateKind = templateKind,
            DotMatrixHeightMode = heightMode
        };
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
